#include <stdio.h>
#include <stdint.h>

typedef uint8_t byte;
typedef uint16_t ushort;

byte RAM[0x0800];
byte ROM[0x8000];

byte Read(ushort Address) {
    if (Address < 0x800) {
        return RAM[Address];
    }
    else if (Address >= 0x8000 && Address <= 0xFFFF) {
        return ROM[Address - 0x8000];
    }
    return 0x00;
}

typedef struct {
    uint16_t ProgramCounter;
    byte a;
    byte x;
    byte y;
} CPU;

int main() {
    CPU CentralProcessingUnit;
}