;--------------------------------------------------------
; File Created by SDCC : free open source ANSI-C Compiler
; Version 4.2.0 #13081 (MINGW64)
;--------------------------------------------------------
	.module Hal_Flash
	.optsdcc -mstm8
	
;--------------------------------------------------------
; Public variables in this module
;--------------------------------------------------------
	.globl _FLASH_GetFlagStatus
	.globl _FLASH_SetProgrammingTime
	.globl _FLASH_ReadByte
	.globl _FLASH_ProgramByte
	.globl _FLASH_Lock
	.globl _FLASH_Unlock
	.globl _InitMemFlash
	.globl _ReadMemFlash
	.globl _WriteMemFlash
;--------------------------------------------------------
; ram data
;--------------------------------------------------------
	.area DATA
;--------------------------------------------------------
; ram data
;--------------------------------------------------------
	.area INITIALIZED
;--------------------------------------------------------
; absolute external ram data
;--------------------------------------------------------
	.area DABS (ABS)

; default segment ordering for linker
	.area HOME
	.area GSINIT
	.area GSFINAL
	.area CONST
	.area INITIALIZER
	.area CODE

;--------------------------------------------------------
; global & static initialisations
;--------------------------------------------------------
	.area HOME
	.area GSINIT
	.area GSFINAL
	.area GSINIT
;--------------------------------------------------------
; Home
;--------------------------------------------------------
	.area HOME
	.area HOME
;--------------------------------------------------------
; code
;--------------------------------------------------------
	.area CODE
;	.\..\src\Hal_Flash.c: 5: void InitMemFlash(void)
;	-----------------------------------------
;	 function InitMemFlash
;	-----------------------------------------
_InitMemFlash:
;	.\..\src\Hal_Flash.c: 8: FLASH_SetProgrammingTime(FLASH_PROGRAMTIME_STANDARD);
	clr	a
;	.\..\src\Hal_Flash.c: 9: }
	jp	_FLASH_SetProgrammingTime
;	.\..\src\Hal_Flash.c: 11: void ReadMemFlash(u32 addr, u8 *dst, u8 len)
;	-----------------------------------------
;	 function ReadMemFlash
;	-----------------------------------------
_ReadMemFlash:
	sub	sp, #3
;	.\..\src\Hal_Flash.c: 18: disableInterrupts();
	sim
;	.\..\src\Hal_Flash.c: 19: for (i=0; i< len; i++)
	clr	(0x03, sp)
00103$:
	ld	a, (0x03, sp)
	cp	a, (0x0c, sp)
	jrnc	00101$
;	.\..\src\Hal_Flash.c: 21: dst[i] = FLASH_ReadByte(addr+i);
	clrw	x
	ld	a, (0x03, sp)
	ld	xl, a
	addw	x, (0x0a, sp)
	ldw	(0x01, sp), x
	ld	a, (0x03, sp)
	clrw	y
	clrw	x
	ld	yl, a
	addw	y, (0x08, sp)
	ld	a, xl
	adc	a, (0x07, sp)
	rlwa	x
	adc	a, (0x06, sp)
	ld	xh, a
	pushw	y
	pushw	x
	call	_FLASH_ReadByte
	ldw	x, (0x01, sp)
	ld	(x), a
;	.\..\src\Hal_Flash.c: 19: for (i=0; i< len; i++)
	inc	(0x03, sp)
	jra	00103$
00101$:
;	.\..\src\Hal_Flash.c: 24: enableInterrupts();
	rim
;	.\..\src\Hal_Flash.c: 25: }
	ldw	x, (4, sp)
	addw	sp, #12
	jp	(x)
;	.\..\src\Hal_Flash.c: 27: void WriteMemFlash(u32 addr, u8 *dst, u8 len)
;	-----------------------------------------
;	 function WriteMemFlash
;	-----------------------------------------
_WriteMemFlash:
	sub	sp, #5
;	.\..\src\Hal_Flash.c: 33: FLASH_Unlock(FLASH_MEMTYPE_DATA);
	ld	a, #0xf7
	call	_FLASH_Unlock
;	.\..\src\Hal_Flash.c: 34: while (FLASH_GetFlagStatus(FLASH_FLAG_DUL) == RESET);
00101$:
	ld	a, #0x08
	call	_FLASH_GetFlagStatus
	tnz	a
	jreq	00101$
;	.\..\src\Hal_Flash.c: 36: disableInterrupts();
	sim
;	.\..\src\Hal_Flash.c: 37: for (i=0; i< len; i++)
	clr	(0x05, sp)
00106$:
	ld	a, (0x05, sp)
	cp	a, (0x0e, sp)
	jrnc	00104$
;	.\..\src\Hal_Flash.c: 39: FLASH_ProgramByte(addr+i, dst[i]);
	clrw	x
	ld	a, (0x05, sp)
	ld	xl, a
	addw	x, (0x0c, sp)
	ld	a, (x)
	clrw	y
	exg	a, yl
	ld	a, (0x05, sp)
	exg	a, yl
	clrw	x
	ldw	(0x01, sp), x
	addw	y, (0x0a, sp)
	ldw	x, (0x01, sp)
	jrnc	00133$
	incw	x
00133$:
	addw	x, (0x08, sp)
	push	a
	pushw	y
	pushw	x
	call	_FLASH_ProgramByte
;	.\..\src\Hal_Flash.c: 37: for (i=0; i< len; i++)
	inc	(0x05, sp)
	jra	00106$
00104$:
;	.\..\src\Hal_Flash.c: 41: FLASH_Lock(FLASH_MEMTYPE_DATA);
	ld	a, #0xf7
	call	_FLASH_Lock
;	.\..\src\Hal_Flash.c: 42: enableInterrupts();
	rim
;	.\..\src\Hal_Flash.c: 43: }
	ldw	x, (6, sp)
	addw	sp, #14
	jp	(x)
	.area CODE
	.area CONST
	.area INITIALIZER
	.area CABS (ABS)
