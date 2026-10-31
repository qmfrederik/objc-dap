#include <Foundation/Foundation.h>

int main()
{
    Class cls = [NSString class];
    Class metaClass = object_getClass([NSString class]);
    Class nilClass = Nil;
    return 0;
}
