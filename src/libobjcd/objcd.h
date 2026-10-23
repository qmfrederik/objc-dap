#ifndef OBJCD_H
#define OBJCD_H

#include <objc/objc.h>

#include "objcd-dap.h"

#if defined(_WIN32)
#ifdef OBJCD_BUILDING
#define OBJCD_API __declspec(dllexport)
#else
#define OBJCD_API __declspec(dllimport)
#endif
#else
#define OBJCD_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

OBJCD_API BOOL is_objc_object(void* obj);

/* Returns NULL if obj is not an Objective-C object; otherwise release with free_objc_variable. */
OBJCD_API objcd_dap_variable* get_objc_variable(void* obj);
OBJCD_API void free_objc_variable(objcd_dap_variable* var);

#ifdef __cplusplus
}
#endif

#endif /* OBJCD_H */
