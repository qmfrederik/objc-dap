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

    // Appending a character outside of the 8-bit encoding switches the storage to 16-bit
    NSMutableString *wideMutableString = [NSMutableString stringWithString:@"Cost: 5 "];
    [wideMutableString appendFormat:@"%C", (unichar)0x20AC];

    // ASCII bytes that are not copied end up in a GSCBufferString
    static const char bufferBytes[] = "Backed by a buffer";
    NSString *bufferString = [[NSString alloc] initWithBytesNoCopy:(void *)bufferBytes
                                                            length:sizeof(bufferBytes) - 1
                                                          encoding:NSASCIIStringEncoding
                                                      freeWhenDone:NO];

    // UTF-16 data is stored as-is in a GSUnicodeBufferString
    static const unichar wideChars[] = { 'P', 'r', 'i', 'c', 'e', ':', ' ', '5', ' ', 0x20AC };
    NSString *wideBufferString = [[NSString alloc] initWithCharactersNoCopy:(unichar *)wideChars
                                                                     length:sizeof(wideChars) / sizeof(unichar)
                                                               freeWhenDone:NO];

    // Characters outside of the 8-bit encoding force 16-bit storage
    NSString *wideInlineString = [NSString stringWithFormat:@"Total: %d %C", 42, (unichar)0x20AC];
    NSString *longString = [NSString stringWithFormat:@"%@", @"The quick brown fox jumps over the lazy dog"];
    NSString *subString = [longString substringWithRange:NSMakeRange(4, 15)];
    NSString *wideLongString = [NSString stringWithFormat:@"%C The quick brown fox jumps over the lazy dog", (unichar)0x20AC];
    NSString *wideSubString = [wideLongString substringWithRange:NSMakeRange(6, 15)];

    // Unbuffered so output appears immediately when stdout is a pipe.
    setvbuf(stdout, NULL, _IONBF, 0);

    printf("emptyString: 0x%p\n", emptyString);
    printf("tinyString: 0x%p\n", tinyString);
    printf("constantStringUTF7: 0x%p\n", constantStringUTF7);
    printf("constantStringUnicode: 0x%p\n", constantStringUnicode);
    printf("formattedString: 0x%p\n", formattedString);
    printf("mutableString: 0x%p\n", mutableString);
    printf("wideMutableString: 0x%p\n", wideMutableString);
    printf("bufferString: 0x%p\n", bufferString);
    printf("wideBufferString: 0x%p\n", wideBufferString);
    printf("wideInlineString: 0x%p\n", wideInlineString);
    printf("subString: 0x%p\n", subString);
    printf("wideSubString: 0x%p\n", wideSubString);
    printf("---\n");

    getchar();
}