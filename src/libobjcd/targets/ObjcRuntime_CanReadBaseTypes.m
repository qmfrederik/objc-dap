#include <Foundation/Foundation.h>

int main()
{
    NSDate *date = [NSDate dateWithTimeIntervalSince1970:1700000000];
    NSURL *url = [NSURL URLWithString:@"https://example.com/path?query=1"];
    NSError *error = [NSError errorWithDomain:NSCocoaErrorDomain
                                         code:4
                                     userInfo:@{NSLocalizedDescriptionKey: @"File not found"}];

    // NSPoint, NSSize and NSRect are C structs, so they are boxed in NSValue objects.
    NSValue *point = [NSValue valueWithPoint:NSMakePoint(1.5, 2.5)];
    NSValue *size = [NSValue valueWithSize:NSMakeSize(640, 480)];
    NSValue *rect = [NSValue valueWithRect:NSMakeRect(10, 20, 300, 200)];

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    printf("date: 0x%p\n", date);
    printf("url: 0x%p\n", url);
    printf("error: 0x%p\n", error);
    printf("point: 0x%p\n", point);
    printf("size: 0x%p\n", size);
    printf("rect: 0x%p\n", rect);
    printf("---\n");

    getchar();
}
