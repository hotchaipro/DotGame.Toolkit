# Copyright (c) Hot Chai Games LLC. Licensed under the MIT License.
#
# check-linux-floor.py <file>...: fails if a Linux binary needs a glibc newer than the oldest one the
# toolkit supports (Ubuntu 22.04's, 2.35, as the framework's tools), or needs libstdc++ at all (glyphgen
# links libc++ statically). It reads the ELF file itself, so it runs on any machine with Python 3.
#
#   python native/check-linux-floor.py native/glyphgen/linux-*/glyphgen
import struct
import sys

FLOOR = (2, 35)
SHT_DYNAMIC = 6
SHT_GNU_VERNEED = 0x6FFFFFFE
DT_NEEDED = 1


def read(path):
    with open(path, 'rb') as f:
        data = f.read()
    if data[:4] != b'\x7fELF' or data[4] != 2 or data[5] != 1:
        raise ValueError('not a 64-bit little-endian ELF file')
    shoff, = struct.unpack_from('<Q', data, 0x28)
    shentsize, shnum = struct.unpack_from('<HH', data, 0x3A)
    sections = [struct.unpack_from('<IIQQQQIIQQ', data, shoff + i * shentsize) for i in range(shnum)]

    def string(section, offset):
        start = sections[section][4] + offset
        return data[start:data.index(b'\0', start)].decode()

    needed, versions = [], []
    for _, kind, _, _, offset, size, link, info, _, entsize in sections:
        if kind == SHT_DYNAMIC:
            for at in range(offset, offset + size, 16):
                tag, value = struct.unpack_from('<qQ', data, at)
                if tag == DT_NEEDED:
                    needed.append(string(link, value))
        elif kind == SHT_GNU_VERNEED:
            at = offset
            for _ in range(info):
                _, count, file, aux, next_need = struct.unpack_from('<HHIII', data, at)
                library = string(link, file)
                a = at + aux
                for _ in range(count):
                    _, _, _, name, next_aux = struct.unpack_from('<IHHII', data, a)
                    versions.append((library, string(link, name)))
                    a += next_aux
                at += next_need
    return needed, versions


def main(paths):
    failed = False
    for path in paths:
        needed, versions = read(path)
        glibc = sorted({tuple(int(p) for p in v[6:].split('.')) for _, v in versions if v.startswith('GLIBC_2')})
        newest = glibc[-1] if glibc else None
        problems = []
        if newest and newest > FLOOR:
            problems.append('needs GLIBC_%s' % '.'.join(map(str, newest)))
        problems += ['needs %s' % n for n in needed if n.startswith('libstdc++')]
        print('%s: glibc %s, needs %s%s' % (path, '.'.join(map(str, newest)) if newest else 'none',
                                         ', '.join(needed) or 'nothing', ': ' + '; '.join(problems) if problems else ''))
        failed |= bool(problems)
    if failed:
        print('error: newer than the floor, glibc %s and no libstdc++' % '.'.join(map(str, FLOOR)))
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
