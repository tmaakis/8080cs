#!/bin/bash
function dumpdiff {
	cmp -l $1 $2 | gawk '{printf "%08X %02X %02X\n", $1, strtonum(0$2), strtonum(0$3)}'
}

"$1" "$2" "$3"
