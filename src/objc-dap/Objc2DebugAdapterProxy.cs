using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Objc.Dap.Protocol;
using Objc.Dap.Runtime;
using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
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

        // Member accesses which wait for lldb to evaluate the variable they start at, by the sequence number of that request.
        private readonly ConcurrentDictionary<int, PendingMemberAccess> pendingMemberAccesses = new ConcurrentDictionary<int, PendingMemberAccess>();

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
            // Whatever the proxy adds is a bonus. A bug, or something unexpected in the debuggee, must never end the
            // debug session, so the message is passed on as it was received instead.
            try
            {
                this.ProxyMessageCore(messageWithHeader, messageBytes, message, target);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.Logger.LogError(ex, "Could not process a {Type} message. It is forwarded unchanged.", message.Type);
                this.ForwardUnchanged(messageWithHeader, message, target);
            }
        }

        private void ProxyMessageCore(ReadOnlySequence<byte> messageWithHeader, ReadOnlySequence<byte> messageBytes, ProtocolMessage message, AsyncQueue<Message?> target)
        {
            if (message is Request request)
            {
                switch (request)
                {
                    case VariablesRequest variablesRequest when this.TryHandleVariablesRequest(variablesRequest):
                        return;

                    case EvaluateRequest evaluateRequest when this.TryEvaluateMemberAccess(evaluateRequest, messageWithHeader):
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
                this.OpenProcess(processEvent.Body?.SystemProcessId);
                base.ProxyMessage(messageWithHeader, messageBytes, message, target);
            }
            else if (message is EvaluateResponse evaluateResponse)
            {
                // The answer to the question the proxy asked itself while evaluating a member access.
                if (this.pendingMemberAccesses.TryRemove(evaluateResponse.RequestSequence, out var pending))
                {
                    this.CompleteMemberAccess(evaluateResponse, pending);
                    return;
                }

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

        // The process id is optional in the protocol, and opening the process can fail, e.g. for a process on another
        // machine or one we have no access to. Without access to the process, we have nothing to add.
        private void OpenProcess(int? processId)
        {
            if (processId == null)
            {
                this.Logger.LogWarning("The process event has no process id, so process memory can't be read.");
                return;
            }

            try
            {
                this.runtime.Open(processId.Value);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or ArgumentException)
            {
                this.Logger.LogWarning(ex, "Could not open process {ProcessId}, so process memory can't be read.", processId);
            }
        }

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

            // Objects can be in any state while debugging. If this one can't be read, show what lldb-dap reported.
            try
            {
                description = this.DescribeObject(@object);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                this.Logger.LogWarning(ex, "Could not read the object at 0x{Address:X}.", @object.Address);
                return false;
            }

            return true;
        }

        private void Describe(Variable variable, Objc2Object @object)
        {
            try
            {
                this.DescribeObject(@object).ApplyTo(variable);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                this.Logger.LogWarning(ex, "Could not read the object at 0x{Address:X}.", @object.Address);
            }
        }

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

        private sealed record PendingMemberAccess(EvaluateRequest Request, byte[] RequestBytes, MemberAccessChain Chain);

        // Why this exists:
        //
        // lldb evaluates `object->ivar` with the wrong offset, so hovering over (or watching) such an expression
        // shows garbage. For `Person *person` with `int age`, `person->age` returns the low half of the isa pointer
        // instead of the age. libobjc2's non-fragile ABI does not store ivar offsets in the binary: each one lives in
        // a variable which the runtime fills in when the class loads, and lldb does not know how to look these up.
        // It treats every ivar as being at offset 0.
        //
        // The runtime does know the real offsets (see Objc2Runtime.GetInstanceVariables), and that is how the Variables
        // pane gets them right. This does the same for expressions which are a chain of member accesses, i.e.
        // `object->child->name`:
        // - lldb is asked for the variable the chain starts at, which it can do (that is only a pointer),
        // - the chain is then followed through the instance variables found in the class metadata,
        // - if that does not work, e.g. for a C struct, the original request is sent on to lldb and its answer is used.
        //
        // Anything which is not exactly such a chain is left to lldb. See MemberAccessChain.
        //
        // This is a workaround, and covers only hover and watch. The debug console, conditional breakpoints and
        // everything else that goes through lldb's expression evaluator still get the wrong values. The proper fix is
        // upstream, so that lldb can find the ivar offsets of libobjc2 classes itself:
        // - In LLDB: its Objective-C language runtime support should resolve ivar offsets for the GNUstep runtime, the
        //   way it already does for Apple's runtime by asking the process. The information is available: a class
        //   points to its ivar list, and every ivar to its offset (see Objc2Runtime.GetInstanceVariables), and the
        //   public runtime API (class_getInstanceVariable, ivar_getOffset) returns the same. Note that we have not
        //   checked what LLDB's GNUstep runtime plugin implements today. It may already have some of the plumbing.
        // - In GNUstep (libobjc2, and clang for the debug information): if ivar offsets cannot be found through the
        //   runtime in a way a debugger can rely on, a documented, stable interface for debuggers would be needed,
        //   similar to what Apple's runtime exports for its debugger. That is for the libobjc2 maintainers to decide.
        // Once LLDB does this, this code, MemberAccessChain and the tests for them can be removed.
        //
        // Returns true if the request was taken over by the proxy and must not be forwarded.
        private bool TryEvaluateMemberAccess(EvaluateRequest request, ReadOnlySequence<byte> messageWithHeader)
        {
            if (request.Arguments?.Context is not (EvaluateArguments.HoverContext or EvaluateArguments.WatchContext)
                || !MemberAccessChain.TryParse(request.Arguments.Expression, out var chain))
            {
                return false;
            }

            var probeSequence = Interlocked.Increment(ref this.sequence);
            this.pendingMemberAccesses[probeSequence] = new PendingMemberAccess(request, messageWithHeader.ToArray(), chain);

            var probe = new EvaluateRequest()
            {
                Sequence = probeSequence,
                Command = EvaluateRequest.CommandName,
                Arguments = new EvaluateArguments()
                {
                    Expression = chain.Root,
                    FrameId = request.Arguments.FrameId,
                    Context = request.Arguments.Context,
                },
            };

            this.ServerQueue.Enqueue(this.CreateMessage(probe, DebugAdapterSerializerContext.Default.EvaluateRequest));
            return true;
        }

        private void CompleteMemberAccess(EvaluateResponse probeResponse, PendingMemberAccess pending)
        {
            // Following the chain reads memory of an object we know little about. If that goes wrong, let lldb answer.
            EvaluateResponse? answer;

            try
            {
                answer = this.TryAnswerMemberAccess(probeResponse, pending);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                this.Logger.LogWarning(ex, "Could not read '{Expression}'. lldb is asked instead.", pending.Request.Arguments?.Expression);
                answer = null;
            }

            if (answer != null)
            {
                this.ClientQueue.Enqueue(this.CreateMessage(answer, DebugAdapterSerializerContext.Default.Response));
                return;
            }

            // Not something we know how to read, so let lldb answer the original request.
            this.TrackEvaluateRequest(pending.Request);

            var owner = this.memoryPool.Rent(pending.RequestBytes.Length);
            var memory = owner.Memory.Slice(0, pending.RequestBytes.Length);
            pending.RequestBytes.CopyTo(memory);
            this.ServerQueue.Enqueue(new Message(owner, memory, pending.Request.Type, pending.Request));
        }

        // Returns null if the member access is not something which can be answered from the class metadata.
        private EvaluateResponse? TryAnswerMemberAccess(EvaluateResponse probeResponse, PendingMemberAccess pending)
        {
            var chain = pending.Chain;
            var body = probeResponse.Body;

            if (!probeResponse.Success
                || body?.Type == null
                || !body.Type.EndsWith(" *")
                || !body.Result.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var container = this.runtime.ReadObject((nuint)GetValueAddress(body.Result));
            var path = chain.Root;

            for (var i = 0; i < chain.Members.Count; i++)
            {
                if (container == Objc2Object.Null)
                {
                    return null;
                }

                var instanceVariable = this.runtime.TryGetInstanceVariables(container)?.FirstOrDefault(v => v.Name == chain.Members[i]);

                if (instanceVariable == null)
                {
                    return null;
                }

                path = $"{path}->{chain.Members[i]}";

                if (i == chain.Members.Count - 1)
                {
                    return this.CreateEvaluateResponse(pending.Request, this.ReadInstanceVariable(container, instanceVariable));
                }

                // Every member but the last must lead to another Objective-C object.
                object? next;

                try
                {
                    next = this.runtime.GetInstanceVariableValue(container, instanceVariable);
                }
                catch (InvalidOperationException)
                {
                    return null;
                }

                if (next == null)
                {
                    // We understand the chain, and know there is nothing to follow, which is not what lldb would tell us.
                    return new EvaluateResponse()
                    {
                        Sequence = Interlocked.Increment(ref this.sequence),
                        RequestSequence = pending.Request.Sequence,
                        Success = false,
                        Message = $"Cannot read '{chain.Members[i + 1]}': {path} is nil.",
                    };
                }

                if (next is not Objc2Object nextObject)
                {
                    return null;
                }

                container = nextObject;
            }

            return null;
        }

        private EvaluateResponse CreateEvaluateResponse(EvaluateRequest request, Variable variable) => new EvaluateResponse()
        {
            Sequence = Interlocked.Increment(ref this.sequence),
            RequestSequence = request.Sequence,
            Success = true,
            Body = new EvaluateResponseBody()
            {
                Result = variable.Value,
                Type = variable.Type,
                VariablesReference = variable.VariablesReference,
                IndexedVariables = variable.IndexedVariables,
                NamedVariables = variable.NamedVariables,
                MemoryReference = variable.MemoryReference,
            },
        };

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
