;--------------------------------------------------------
; File Created by SDCC : free open source ANSI-C Compiler
; Version 4.2.0 #13081 (MINGW64)
;--------------------------------------------------------
	.module APP_PassiveSwitch
	.optsdcc -mstm8
	
;--------------------------------------------------------
; Public variables in this module
;--------------------------------------------------------
	.globl _DoClickAction
	.globl _GetTelFrameKeyState
	.globl _SearchTelfromTable
	.globl _DoSMNormalPassiveProcess
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
;	.\..\src\APP_PassiveSwitch.c: 11: void DoSMNormalPassiveProcess(u8 ch, const TEL_RPS_1BS_TYPE * ptr_tel, DEVICE_ID_TYPE *ptr_dev)
;	-----------------------------------------
;	 function DoSMNormalPassiveProcess
;	-----------------------------------------
_DoSMNormalPassiveProcess:
	sub	sp, #9
	ld	(0x09, sp), a
	ldw	(0x07, sp), x
;	.\..\src\APP_PassiveSwitch.c: 16: matched_num = SearchTelfromTable(ch, ptr_tel, index_of_identry, &out);
	ldw	x, sp
	addw	x, #5
	pushw	x
	ldw	x, sp
	addw	x, #3
	pushw	x
	ldw	x, (0x0b, sp)
	ld	a, (0x0d, sp)
	call	_SearchTelfromTable
;	.\..\src\APP_PassiveSwitch.c: 17: if (matched_num && (ch < MAX_NUM_OF_ID_TABLE) && (GetTelFrameKeyState(ptr_tel) == KEY_PRESSED))
	tnz	a
	jreq	00105$
	ld	a, (0x09, sp)
	cp	a, #0x01
	jrnc	00105$
	ldw	x, (0x07, sp)
	call	_GetTelFrameKeyState
	dec	a
	jrne	00105$
;	.\..\src\APP_PassiveSwitch.c: 25: DoClickAction(ch, out, TEL_FARME_REPORT_EVENT_PASSIVE);
	push	#0x83
	ldw	x, (0x06, sp)
	ld	a, (0x0a, sp)
	call	_DoClickAction
00105$:
;	.\..\src\APP_PassiveSwitch.c: 28: }
	ldw	x, (10, sp)
	addw	sp, #13
	jp	(x)
	.area CODE
	.area CONST
	.area INITIALIZER
	.area CABS (ABS)
