#include <Foundation/Foundation.h>

int main()
{
    // See https://github.com/gnustep/libs-base/blob/master/Source/NSData.m
    static const unsigned char staticBytes[] = { 0xca, 0xfe, 0xba, 0xbe };
    unsigned char largeBytes[100];

    for (int i = 0; i < sizeof(largeBytes); i++)
    {
        largeBytes[i] = (unsigned char)i;
    }

    NSData *emptyData = [NSData data];
    NSData *staticData = [NSData dataWithBytesNoCopy:(void *)staticBytes length:sizeof(staticBytes) freeWhenDone:NO];
    NSData *smallData = [NSData dataWithBytes:"\x01\x02\x03" length:3];
    NSData *exactPreviewData = [NSData dataWithBytes:largeBytes length:16];
    NSData *largeData = [NSData dataWithBytes:largeBytes length:sizeof(largeBytes)];
    NSMutableData *mutableData = [NSMutableData dataWithBytes:largeBytes length:5];
    NSMutableData *emptyMutableData = [NSMutableData data];

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    printf("emptyData: 0x%p\n", emptyData);
    printf("staticData: 0x%p\n", staticData);
    printf("smallData: 0x%p\n", smallData);
    printf("exactPreviewData: 0x%p\n", exactPreviewData);
    printf("largeData: 0x%p\n", largeData);
    printf("mutableData: 0x%p\n", mutableData);
    printf("emptyMutableData: 0x%p\n", emptyMutableData);
    printf("---\n");

    getchar();
}
