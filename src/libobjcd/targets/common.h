#include <inttypes.h>
#include <stdint.h>
#include <stdio.h>

// Prints "<variable name>: 0x<address>", using the same format on every platform.
#define PRINT_ADDRESS(var) printf(#var ": 0x%" PRIxPTR "\n", (uintptr_t)(var))
