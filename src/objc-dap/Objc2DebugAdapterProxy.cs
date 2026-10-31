using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Objc.Dap.Protocol;
using Objc.Dap.Runtime;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Objc.Dap
{
    public class Objc2DebugAdapterProxy : DebugAdapterProxy
    {
        // Requests after which the debuggee runs or goes away, ending the lifetime of variable references.
        private static readonly HashSet<string> ReferenceInvalidatingCommands = new HashSet<string>(StringComparer.Ordinal)
        {
            "continue",
            "next",
            "stepIn",
            "stepOut",
            "stepBack",
            "reverseContinue",
            "restartFrame",
            "goto",
            "restart",
            "terminate",
            "disconnect",
        };

        private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;

        private readonly Objc2Runtime runtime;

        private readonly VariableReferenceStore references;

        // The sequence numbers of the evaluate requests whose results are shown as variables. Requests and responses
        // are proxied on different threads.
        private readonly HashSet<int> evaluateRequests = new HashSet<int>();
        private readonly Lock evaluateRequestsLock = new Lock();

        // lldb-dap sequence numbers are small; clients correlate responses by request_seq.
        private int sequence = VariableReferenceStore.FirstReference;

        public Objc2DebugAdapterProxy(DebugAdapterOptions options, Objc2Runtime runtime, VariableReferenceStore references, ILogger<DebugAdapterProxy> logger)
            : base(options, logger)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.references = references ?? throw new ArgumentNullException(nameof(references));
        }

        protected override void ProxyMessage(ReadOnlySequence<byte> messageWithHeader, ReadOnlySequence<byte> messageBytes, ProtocolMessage message, AsyncQueue<Message?> target)
        {
            if (message is Request request)
            {
                switch (request)
                {
                    case VariablesRequest variablesRequest when this.TryHandleVariablesRequest(variablesRequest):
                        return;

                    case EvaluateRequest evaluateRequest:
                        this.TrackEvaluateRequest(evaluateRequest);
                        break;

                    default:
                        if (ReferenceInvalidatingCommands.Contains(request.Command))
                        {
                            this.InvalidateReferences();
                        }

                        break;
                }

                base.ProxyMessage(messageWithHeader, messageBytes, message, target);
            }
            else if (message is ProcessEvent)
            {
                var processEvent = (ProcessEvent)message;
                this.InvalidateReferences();
                this.runtime.Open(processEvent.Body.SystemProcessId!.Value);
                base.ProxyMessage(messageWithHeader, messageBytes, message, target);
            }
            else if (message is EvaluateResponse evaluateResponse)
            {
                // Only the responses to hover and watch requests are tracked.
                if (this.TryUntrackEvaluateRequest(evaluateResponse.RequestSequence)
                    && evaluateResponse.Success
                    && evaluateResponse.Body != null
                    && this.TryDescribe(evaluateResponse.Body.Type, evaluateResponse.Body.Result, out var description))
                {
                    description.ApplyTo(evaluateResponse.Body);
                    target.Enqueue(this.CreateMessage(evaluateResponse, DebugAdapterSerializerContext.Default.Response));
                }
                else
                {
                    base.ProxyMessage(messageWithHeader, messageBytes, message, target);
                }
            }
            else if (message is VariablesResponse)
            {
                var variablesResponse = (VariablesResponse)message;

                var variables = variablesResponse.Body.Variables;

                foreach (var variable in variables)
                {
                    if (this.TryDescribe(variable.Type, variable.Value, out var description))
                    {
                        description.ApplyTo(variable);
                    }
                }

                var variablesResponseMessage = this.CreateMessage(variablesResponse, DebugAdapterSerializerContext.Default.Response);
#if DEBUG
                var variablesResponseString = Encoding.UTF8.GetString(variablesResponseMessage.Value.Span);
                var inputString = Encoding.UTF8.GetString(messageWithHeader);
#endif
                target.Enqueue(variablesResponseMessage);
            }
            else
            {
                base.ProxyMessage(messageWithHeader, messageBytes, message, target);
            }
        }

        private void InvalidateReferences() => this.references.Clear();

        // Describes a value reported by lldb-dap, given its type and its value, which is an address.
        // Returns false if the value is not a pointer to something that can be read.
        private bool TryDescribe(string? type, string? value, [NotNullWhen(true)] out Objc2ObjectDescription? description)
        {
            description = null;

            if (type == null || value == null || !value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var address = GetValueAddress(value);

            // lldb-dap reports Class variables as pointers to objc_class. They must not be read as instances.
            if (type is "Class" or "objc_class *")
            {
                if (address == 0)
                {
                    description = new Objc2ObjectDescription() { Type = "Class", Value = "nil" };
                }
                else if (this.runtime.TryReadClass((nuint)address, out var @class))
                {
                    description = new Objc2ObjectDescription() { Type = "Class", Value = this.runtime.GetClassName(@class) ?? "<unreadable>" };
                }

                return description != null;
            }

            if (!type.EndsWith(" *"))
            {
                return false;
            }

            var @object = this.runtime.ReadObject((nuint)address);

            if (@object == Objc2Object.Null)
            {
                return false;
            }

            description = this.DescribeObject(@object);
            return true;
        }

        private void Describe(Variable variable, Objc2Object @object) => this.DescribeObject(@object).ApplyTo(variable);

        private Objc2ObjectDescription DescribeObject(Objc2Object @object)
        {
            var info = this.runtime.DescribeObject(@object);
            var className = info.ClassName;

            string? text = info.Value?.ToString();

            // Collections have no value of their own, so summarize them with their class and size.
            if (info.Value == null && info.ItemCount >= 0 && className != null)
            {
                text = $"{className} <{info.ItemCount} {(info.ItemCount == 1 ? "item" : "items")}>";
            }
            else if (info.Value == null && className != null)
            {
                text = $"<{className}: 0x{@object.Address:X}>";
            }

            // An empty collection has nothing to expand.
            var expandable = info.ItemCount != 0 && (info.IndexedVariablesCount >= 0 || info.NamedVariablesCount >= 0);

            return new Objc2ObjectDescription()
            {
                Type = className != null ? $"{className} *" : null,
                Value = text,

                // Point the binary viewer at the buffer, not at the variable that holds the NSData pointer.
                MemoryReference = info.DataBuffer != null ? $"0x{info.DataBuffer:X}" : null,
                VariablesReference = expandable ? this.references.Add(@object.Address) : 0,
                IndexedVariables = Math.Max(info.IndexedVariablesCount, 0),
                NamedVariables = Math.Max(info.NamedVariablesCount, 0),
            };
        }

        private void TrackEvaluateRequest(EvaluateRequest request)
        {
            // Hover and watch show the result as a variable. The repl shows it as text, and clipboard needs it unchanged.
            if (request.Arguments?.Context is EvaluateArguments.HoverContext or EvaluateArguments.WatchContext)
            {
                lock (this.evaluateRequestsLock)
                {
                    this.evaluateRequests.Add(request.Sequence);
                }
            }
        }

        private bool TryUntrackEvaluateRequest(int sequence)
        {
            lock (this.evaluateRequestsLock)
            {
                return this.evaluateRequests.Remove(sequence);
            }
        }

        // Returns true if the request referred to a proxy-owned reference and was answered here.
        private bool TryHandleVariablesRequest(VariablesRequest request)
        {
            if (request.Arguments == null || !this.references.TryGetAddress(request.Arguments.VariablesReference, out var address))
            {
                return false;
            }

            var response = this.CreateVariablesResponse(request, request.Arguments, address);
            this.ClientQueue.Enqueue(this.CreateMessage(response, DebugAdapterSerializerContext.Default.Response));
            return true;
        }

        private VariablesResponse CreateVariablesResponse(VariablesRequest request, VariablesArguments arguments, nuint address)
        {
            var response = new VariablesResponse()
            {
                Sequence = Interlocked.Increment(ref this.sequence),
                RequestSequence = request.Sequence,
                Success = true,
                Body = new VariablesResponseBody() { Variables = [] },
            };

            var container = this.runtime.ReadObject(address);
            var indexedCount = this.runtime.GetIndexedVariablesCount(container);
            var namedCount = this.runtime.GetNamedVariablesCount(container);
            IReadOnlyList<Objc2InstanceVariable>? instanceVariables = null;

            if (indexedCount < 0 && namedCount < 0)
            {
                instanceVariables = this.runtime.TryGetInstanceVariables(container);
                namedCount = instanceVariables?.Count ?? -1;
            }

            if (indexedCount < 0 && namedCount < 0)
            {
                response.Success = false;
                response.Message = "The object is no longer available.";
                return response;
            }

            var variables = new List<Variable>();

            if (arguments.Filter != "named")
            {
                (int start, int end) = GetPage(arguments, indexedCount);
                for (int i = start; i < end; i++)
                {
                    variables.Add(this.ReadIndexedVariable(container, i));
                }
            }

            if (arguments.Filter != "indexed")
            {
                (int start, int end) = GetPage(arguments, namedCount);
                for (int i = start; i < end; i++)
                {
                    variables.Add(instanceVariables != null
                        ? this.ReadInstanceVariable(container, instanceVariables[i])
                        : this.ReadNamedVariable(container, i));
                }
            }

            response.Body.Variables = variables.ToArray();
            return response;
        }

        private static (int Start, int End) GetPage(VariablesArguments arguments, int total)
        {
            total = Math.Max(total, 0);
            int start = Math.Clamp(arguments.Start ?? 0, 0, total);
            long end = arguments.Count is > 0 ? Math.Min(total, (long)start + arguments.Count.Value) : total;
            return (start, (int)end);
        }

        private Variable ReadIndexedVariable(Objc2Object array, int index)
        {
            var name = $"[{index}]";

            try
            {
                return this.CreateChildVariable(name, this.runtime.GetIndexedVariable(array, index));
            }
            catch (InvalidOperationException)
            {
                return this.CreateChildVariable(name, Objc2Object.Null);
            }
        }

        private Variable ReadNamedVariable(Objc2Object dictionary, int index)
        {
            try
            {
                (var name, var value) = this.runtime.GetNamedVariable(dictionary, index);
                return this.CreateChildVariable(name, value);
            }
            catch (InvalidOperationException)
            {
                return this.CreateChildVariable($"[{index}]", Objc2Object.Null);
            }
        }

        private Variable ReadInstanceVariable(Objc2Object container, Objc2InstanceVariable instanceVariable)
        {
            var variable = new Variable()
            {
                Name = instanceVariable.Name,
                Type = instanceVariable.TypeName,
                Value = "<unreadable>",
            };

            object? value;
            try
            {
                value = this.runtime.GetInstanceVariableValue(container, instanceVariable);
            }
            catch (InvalidOperationException)
            {
                return variable;
            }

            switch (value)
            {
                case null:
                    variable.Value = "nil";
                    break;

                case Objc2Object child:
                    if (child != Objc2Object.Null)
                    {
                        variable.Value = $"0x{child.Address:X}";
                        this.Describe(variable, child);
                    }

                    break;

                case Objc2Class @class:
                    variable.Value = this.runtime.GetClassName(@class) ?? "<unreadable>";
                    break;

                case nuint pointer:
                    variable.Value = $"0x{pointer:X}";

                    if (pointer != 0)
                    {
                        variable.MemoryReference = variable.Value;
                    }

                    break;

                case byte[] bytes:
                    variable.Value = BitConverter.ToString(bytes).Replace('-', ' ');
                    break;

                case bool flag:
                    variable.Value = flag ? "true" : "false";
                    break;

                default:
                    variable.Value = Convert.ToString(value, CultureInfo.InvariantCulture)!;
                    break;
            }

            return variable;
        }

        private Variable CreateChildVariable(string name, Objc2Object child)
        {
            var variable = new Variable()
            {
                Name = name,
                Type = "id",
                Value = "<unreadable>",
            };

            if (child != Objc2Object.Null)
            {
                variable.Value = $"0x{child.Address:X}";
                this.Describe(variable, child);
            }

            return variable;
        }

        private static ulong GetValueAddress(string value)
        {
            ReadOnlySpan<char> reference = value;
            if (reference.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                reference = reference.Slice(2);
            }

            // A missing or malformed reference maps to the null address.
            return ulong.TryParse(reference, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong address) ? address : 0;
        }

        private Message CreateMessage<T>(T message, JsonTypeInfo<T> jsonTypeInfo)
            where T : ProtocolMessage
        {
            using (MemoryStream stream = new MemoryStream())
            {
                JsonSerializer.Serialize(stream, message, jsonTypeInfo);
                var header = $"Content-Length: {stream.Length}\r\n\r\n";
                var headerLength = Encoding.ASCII.GetByteCount(header);

                var memoryOwner = this.memoryPool.Rent(headerLength + (int)stream.Length);
                var memory = memoryOwner.Memory.Slice(0, headerLength + (int)stream.Length);

                Encoding.ASCII.GetBytes(header, memory.Slice(0, headerLength).Span);
                stream.Position = 0;
                stream.ReadExactly(memory.Slice(headerLength, (int)stream.Length).Span);

                return new Message(memoryOwner, memory, message.Type, message);
            }
        }
    }
}
