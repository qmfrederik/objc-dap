#include <Foundation/Foundation.h>

@interface Person : NSObject
{
@public
    int age;
    NSString *name;
}
@end

@implementation Person
@end

int main()
{
    Person *person = [Person new];
    person->age = 42;
    person->name = @"Douglas Adams";
    return 0;
}
