#ifndef OBJCD_H
#define OBJCD_H

#include <objc/objc.h>

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

#ifdef __cplusplus
}
#endif

#endif /* OBJCD_H */
