#include <Foundation/Foundation.h>

int main()
{
    // Instantiate various type of string objects.  See
    // https://github.com/gnustep/libs-base/blob/master/Source/GSString.m
    // for all details
    NSString *emptyString = @""; // This becomes a compile-time constant
    NSString *tinyString =@"Hi!";
    NSString *constantStringUTF7 = @"Hello, World!";
    NSString *constantStringUnicode = @"Fichier non trouvé";
    NSString *formattedString = [NSString stringWithFormat:@"Hello, %@", @"World!"];
    NSMutableString *mutableString = [NSMutableString stringWithString:@"You can change me"];

    // ASCII bytes that are not copied end up in a GSCBufferString
    static const char bufferBytes[] = "Backed by a buffer";
    NSString *bufferString = [[NSString alloc] initWithBytesNoCopy:(void *)bufferBytes
                                                            length:sizeof(bufferBytes) - 1
                                                          encoding:NSASCIIStringEncoding
                                                      freeWhenDone:NO];

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    printf("emptyString: 0x%p\n", emptyString);
    printf("tinyString: 0x%p\n", tinyString);
    printf("constantStringUTF7: 0x%p\n", constantStringUTF7);
    printf("constantStringUnicode: 0x%p\n", constantStringUnicode);
    printf("formattedString: 0x%p\n", formattedString);
    printf("mutableString: 0x%p\n", mutableString);
    printf("bufferString: 0x%p\n", bufferString);
    printf("---\n");

    getchar();
}