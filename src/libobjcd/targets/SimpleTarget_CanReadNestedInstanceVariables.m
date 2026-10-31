#include <Foundation/Foundation.h>

@interface Person : NSObject
{
@public
    int age;
    NSString *name;
    Person *friend;
}
@end

@implementation Person
@end

struct Point
{
    int x;
    int y;
};

int main()
{
    Person *person = [Person new];
    person->age = 42;
    person->name = @"Douglas Adams";

    Person *friend = [Person new];
    friend->age = 43;
    friend->name = @"Ford Prefect";
    person->friend = friend;

    struct Point *point = malloc(sizeof(struct Point));
    point->x = 5;
    point->y = 6;
    return 0;
}
