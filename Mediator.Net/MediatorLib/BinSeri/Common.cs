using System;
using System.Collections.Generic;
using System.Text;

namespace Ifak.Fast.Mediator.BinSeri
{
    internal static class Common
    {
        internal static readonly char[] mapCode2Char = new char[] {
            '0', // 0
            '1', // 1
            '2', // 2
            '3', // 3
            '4', // 4
            '5', // 5
            '6', // 6
            '7', // 7
            '8', // 8
            '9', // 9
            '.', // 10, 0x0A
            '+', // 11, 0x0B
            '-', // 12, 0x0C
            'E', // 13, 0x0D
            'e', // 14, 0x0E
            ' ', // 15, 0x0F
        };

        internal static readonly byte[] mCodeTable = new byte[0x80] {
            0xFF, // 0
            0xFF, // 1
            0xFF, // 2
            0xFF, // 3
            0xFF, // 4
            0xFF, // 5
            0xFF, // 6
            0xFF, // 7
            0xFF, // 8
            0xFF, // 9
            0xFF, // 10
            0xFF, // 11
            0xFF, // 12
            0xFF, // 13
            0xFF, // 14
            0xFF, // 15
            0xFF, // 16
            0xFF, // 17
            0xFF, // 18
            0xFF, // 19
            0xFF, // 20
            0xFF, // 21
            0xFF, // 22
            0xFF, // 23
            0xFF, // 24
            0xFF, // 25
            0xFF, // 26
            0xFF, // 27
            0xFF, // 28
            0xFF, // 29
            0xFF, // 30
            0xFF, // 31
            0xFF, // 32
            0xFF, // 33
            0xFF, // 34
            0xFF, // 35
            0xFF, // 36
            0xFF, // 37
            0xFF, // 38
            0xFF, // 39
            0xFF, // 40
            0xFF, // 41
            0xFF, // 42
            11,   // 43 = '+'
            0xFF, // 44
            12,   // 45 = '-'
            10,   // 46 = '.'
            0xFF, // 47
            0,    // 48 = '0'
            1,    // 49 = '1'
            2,    // 50 = '2'
            3,    // 51 = '3'
            4,    // 52 = '4'
            5,    // 53 = '5'
            6,    // 54 = '6'
            7,    // 55 = '7'
            8,    // 56 = '8'
            9,    // 57 = '9'
            0xFF, // 58
            0xFF, // 59
            0xFF, // 60
            0xFF, // 61
            0xFF, // 62
            0xFF, // 63
            0xFF, // 64
            0xFF, // 65
            0xFF, // 66
            0xFF, // 67
            0xFF, // 68
            13,   // 69 = 'E'
            0xFF, // 70
            0xFF, // 71
            0xFF, // 72
            0xFF, // 73
            0xFF, // 74
            0xFF, // 75
            0xFF, // 76
            0xFF, // 77
            0xFF, // 78
            0xFF, // 79
            0xFF, // 80
            0xFF, // 81
            0xFF, // 82
            0xFF, // 83
            0xFF, // 84
            0xFF, // 85
            0xFF, // 86
            0xFF, // 87
            0xFF, // 88
            0xFF, // 89
            0xFF, // 90
            0xFF, // 91
            0xFF, // 92
            0xFF, // 93
            0xFF, // 94
            0xFF, // 95
            0xFF, // 96
            0xFF, // 97
            0xFF, // 98
            0xFF, // 99
            0xFF, // 100
            14,   // 101 = 'e'
            0xFF, // 102
            0xFF, // 103
            0xFF, // 104
            0xFF, // 105
            0xFF, // 106
            0xFF, // 107
            0xFF, // 108
            0xFF, // 109
            0xFF, // 110
            0xFF, // 111
            0xFF, // 112
            0xFF, // 113
            0xFF, // 114
            0xFF, // 115
            0xFF, // 116
            0xFF, // 117
            0xFF, // 118
            0xFF, // 119
            0xFF, // 120
            0xFF, // 121
            0xFF, // 122
            0xFF, // 123
            0xFF, // 124
            0xFF, // 125
            0xFF, // 126
            0xFF  // 127
        };
    }
}
