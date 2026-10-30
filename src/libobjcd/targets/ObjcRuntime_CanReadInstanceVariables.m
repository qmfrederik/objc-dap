#include <Foundation/Foundation.h>

@interface Base : NSObject
{
@public
    int baseInt;
}
@end

@implementation Base
@end

@interface Derived : Base
{
@public
    double derivedDouble;
    NSString *derivedString;
    char derivedChar;
}
@end

@implementation Derived
@end

// The offsets reported by the runtime are the reference for the ones read from process memory.
static void printOffset(const char *name)
{
    Ivar ivar = class_getInstanceVariable([Derived class], name);
    printf("%s: 0x%llx\n", name, (unsigned long long)ivar_getOffset(ivar));
}

int main()
{
    Derived *derived = [Derived new];
    derived->derivedString = @"text";

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    printf("derived: 0x%p\n", derived);
    printOffset("baseInt");
    printOffset("derivedDouble");
    printOffset("derivedString");
    printOffset("derivedChar");
    printf("---\n");

    getchar();
}
