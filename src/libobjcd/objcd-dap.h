#ifndef OBJCD_DAP_H
#define OBJCD_DAP_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* Optional strings are NULL when absent. Optional integers are paired with a has_* flag. */

/**
 * Properties of a variable that can be used to determine how to render the
 * variable in the UI.
 */
typedef struct objcd_dap_variable_presentation_hint {
  /**
   * The kind of variable. Before introducing additional values, try to use the
   * listed values.
   *
   * Values:
   * 'property': Indicates that the object is a property.
   * 'method': Indicates that the object is a method.
   * 'class': Indicates that the object is a class.
   * 'data': Indicates that the object is data.
   * 'event': Indicates that the object is an event.
   * 'baseClass': Indicates that the object is a base class.
   * 'innerClass': Indicates that the object is an inner class.
   * 'interface': Indicates that the object is an interface.
   * 'mostDerivedClass': Indicates that the object is the most derived class.
   * 'virtual': Indicates that the object is virtual, that means it is a
   *   synthetic object introduced by the adapter for rendering purposes, e.g.
   *   an index range for large arrays.
   * 'dataBreakpoint': Deprecated: Indicates that a data breakpoint is
   *   registered for the object. The `hasDataBreakpoint` attribute should
   *   generally be used instead.
   */
  const char *kind;

  /**
   * Set of attributes represented as an array of strings. Before introducing
   * additional values, try to use the listed values.
   *
   * Values:
   * 'static': Indicates that the object is static.
   * 'constant': Indicates that the object is a constant.
   * 'readOnly': Indicates that the object is read only.
   * 'rawString': Indicates that the object is a raw string.
   * 'hasObjectId': Indicates that the object has an Object ID associated with
   *   it. This is a vestigial attribute that is used by some clients; 'Object
   *   ID's are not specified in the protocol.
   * 'canHaveObjectId': Indicates that the object can have an Object ID created
   *   for it. This is a vestigial attribute that is used by some clients;
   *   'Object ID's are not specified in the protocol.
   * 'hasSideEffects': Indicates that the evaluation had side effects.
   * 'hasDataBreakpoint': Indicates that the object has its value tracked by a
   *   data breakpoint.
   */
  const char **attributes;
  size_t attributes_count;

  /**
   * Visibility of variable. Before introducing additional values, try to use
   * the listed values.
   *
   * Values: 'public', 'private', 'protected', 'internal', 'final'
   */
  const char *visibility;

  /**
   * If true, clients can present the variable with a UI that supports a
   * specific gesture to trigger its evaluation.
   * This mechanism can be used for properties that require executing code when
   * retrieving their value and where the code execution can be expensive
   * and/or produce side-effects. A typical example are properties based on a
   * getter function.
   * Please note that in addition to the `lazy` flag, the variable's
   * `variablesReference` is expected to refer to a variable that will provide
   * the value through another `variable` request.
   */
  bool lazy;
} objcd_dap_variable_presentation_hint;

/**
 * A Variable is a name/value pair.
 * The `type` attribute is shown if space permits or when hovering over the
 * variable's name.
 * The `kind` attribute is used to render additional properties of the
 * variable, e.g. different icons can be used to indicate that a variable is
 * public or private.
 * If the value is structured (has children), a handle is provided to retrieve
 * the children with the `variables` request.
 * If the number of named or indexed children is large, the numbers should be
 * returned via the `namedVariables` and `indexedVariables` attributes.
 * The client can use this information to present the children in a paged UI
 * and fetch them in chunks.
 */
typedef struct objcd_dap_variable {
  /** The variable's name. */
  const char *name;

  /**
   * The variable's value.
   * This can be a multi-line text, e.g. for a function the body of a function.
   * For structured variables (which do not have a simple value), it is
   * recommended to provide a one-line representation of the structured object.
   * This helps to identify the structured object in the collapsed state when
   * its children are not yet visible.
   * An empty string can be used if no value should be shown in the UI.
   */
  const char *value;

  /**
   * The type of the variable's value. Typically shown in the UI when hovering
   * over the value.
   * This attribute should only be returned by a debug adapter if the
   * corresponding capability `supportsVariableType` is true.
   */
  const char *type;

  /**
   * Properties of a variable that can be used to determine how to render the
   * variable in the UI.
   */
  const objcd_dap_variable_presentation_hint *presentation_hint;

  /**
   * The evaluatable name of this variable which can be passed to the
   * `evaluate` request to fetch the variable's value.
   */
  const char *evaluate_name;

  /**
   * If `variablesReference` is > 0, the variable is structured and its
   * children can be retrieved by passing `variablesReference` to the
   * `variables` request as long as execution remains suspended. See 'Lifetime
   * of Object References' in the Overview section for details.
   */
  int64_t variables_reference;

  /**
   * The number of named child variables.
   * The client can use this information to present the children in a paged UI
   * and fetch them in chunks.
   */
  int64_t named_variables;
  bool has_named_variables;

  /**
   * The number of indexed child variables.
   * The client can use this information to present the children in a paged UI
   * and fetch them in chunks.
   */
  int64_t indexed_variables;
  bool has_indexed_variables;

  /**
   * A memory reference associated with this variable.
   * For pointer type variables, this is generally a reference to the memory
   * address contained in the pointer.
   * For executable data, this reference may later be used in a `disassemble`
   * request.
   * This attribute may be returned by a debug adapter if corresponding
   * capability `supportsMemoryReferences` is true.
   */
  const char *memory_reference;

  /**
   * A reference that allows the client to request the location where the
   * variable is declared. This should be present only if the adapter is likely
   * to be able to resolve the location.
   *
   * This reference shares the same lifetime as the `variablesReference`. See
   * 'Lifetime of Object References' in the Overview section for details.
   */
  int64_t declaration_location_reference;
  bool has_declaration_location_reference;

  /**
   * A reference that allows the client to request the location where the
   * variable's value is declared. For example, if the variable contains a
   * function pointer, the adapter may be able to look up the function's
   * location. This should be present only if the adapter is likely to be able
   * to resolve the location.
   *
   * This reference shares the same lifetime as the `variablesReference`. See
   * 'Lifetime of Object References' in the Overview section for details.
   */
  int64_t value_location_reference;
  bool has_value_location_reference;
} objcd_dap_variable;

#endif /* OBJCD_DAP_H */
