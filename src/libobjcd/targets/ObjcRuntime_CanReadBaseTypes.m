#include <Foundation/Foundation.h>
#include "common.h"

int main()
{
    // 2023-11-14 22:13:20 UTC
    NSDate *date = [NSDate dateWithTimeIntervalSince1970:1700000000];
    NSURL *url = [NSURL URLWithString:@"https://example.com/path?query=1"];

    // NSPoint, NSSize and NSRect are C structs, so they are boxed in NSValue objects.
    NSValue *point = [NSValue valueWithPoint:NSMakePoint(1.5, 2.5)];
    NSValue *size = [NSValue valueWithSize:NSMakeSize(640, 480)];
    NSValue *rect = [NSValue valueWithRect:NSMakeRect(10, 20, 300, 200)];
    NSValue *range = [NSValue valueWithRange:NSMakeRange(5, 10)];

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    PRINT_ADDRESS(date);
    PRINT_ADDRESS(url);
    PRINT_ADDRESS(point);
    PRINT_ADDRESS(size);
    PRINT_ADDRESS(rect);
    PRINT_ADDRESS(range);
    printf("---\n");

    getchar();
}
