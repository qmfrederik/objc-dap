#include "objcd.h"
#include "objcd-dap.h"
#include <Foundation/Foundation.h>
#include <inttypes.h>
#include <stdio.h>
#include <string.h>

#ifdef _WIN32
static BOOL is_readable(const void* p)
{
    MEMORY_BASIC_INFORMATION mbi;
    if (!VirtualQuery(p, &mbi, sizeof mbi))
        return NO;
    return mbi.State == MEM_COMMIT && !(mbi.Protect & (PAGE_NOACCESS | PAGE_GUARD));
}
#else
// write() from p into a pipe fails with EFAULT instead of faulting if p is unreadable.
static BOOL is_readable(const void* p)
{
    static int fds[2] = { -1, -1 };
    if (fds[0] < 0) {
        if (pipe(fds) != 0) {
            fds[0] = fds[1] = -1;
            return NO;
        }
        fcntl(fds[0], F_SETFL, O_NONBLOCK);
        fcntl(fds[1], F_SETFL, O_NONBLOCK);
    }
    char c;
    if (write(fds[1], p, 1) != 1)
        return NO;
    return read(fds[0], &c, 1) == 1;
}
#endif

static BOOL is_registered_class(Class cls)
{
    int n = objc_getClassList(NULL, 0);
    if (n <= 0)
        return NO;
    Class* list = (Class*)malloc(n * sizeof *list);
    if (!list)
        return NO;
    n = objc_getClassList(list, n);
    BOOL found = NO;
    for (int i = 0; i < n && !found; i++)
        found = (list[i] == cls);
    free(list);
    return found;
}

// Heuristic: small objects are tagged pointers; otherwise isa must be a registered class.
BOOL is_objc_object(void* obj)
{
    if (!obj)
        return NO;
    if ((uintptr_t)obj & OBJC_SMALL_OBJECT_MASK)
        return YES;
    if (!is_readable(obj) || !is_readable((char*)obj + sizeof(void*) - 1))
        return NO;
    Class cls = object_getClass((id)obj);
    return cls && is_readable(cls) && is_registered_class(cls);
}

objcd_dap_variable* get_objc_variable(void* obj)
{
    if (!is_objc_object(obj))
        return NULL;
    objcd_dap_variable* var = (objcd_dap_variable*)calloc(1, sizeof *var);
    if (!var)
        return NULL;
    @autoreleasepool {
        id o = (id)obj;
        const char* description = NULL;
        // -description runs arbitrary target code and may throw.
        @try {
            description = [[o description] UTF8String];
        } @catch (id) {
        }
        var->value = _strdup(description ? description : "");
        var->type = _strdup(class_getName(object_getClass(o)));
    }
    char address[2 + 2 * sizeof(uintptr_t) + 1];
    snprintf(address, sizeof address, "0x%" PRIxPTR, (uintptr_t)obj);
    var->memory_reference = _strdup(address);
    return var;
}

void free_objc_variable(objcd_dap_variable* var)
{
    if (!var)
        return;
    free((void*)var->value);
    free((void*)var->type);
    free((void*)var->memory_reference);
    free(var);
}