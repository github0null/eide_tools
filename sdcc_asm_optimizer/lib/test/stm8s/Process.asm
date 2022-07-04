;--------------------------------------------------------
; File Created by SDCC : free open source ANSI-C Compiler
; Version 4.1.0 #12072 (MINGW64)
;--------------------------------------------------------
	.module Process
	.optsdcc -mstm8
	
;--------------------------------------------------------
; Public variables in this module
;--------------------------------------------------------
	.globl _SetRelayAndLedState
	.globl _DoSMNormalPassiveProcess
	.globl _SetLEDStatus
	.globl _SetLED
	.globl _GetRelayStatus
	.globl _RelayControl
	.globl _GetTelFrameKeyState
	.globl _GetTelRCTimes
	.globl _SearchTelfromTable
	.globl _AddIdEntryOverlay
	.globl _ResetIDTable
	.globl _TransTeltoSingleIdEntry
	.globl _ExitMode
	.globl _StartMode
	.globl _GetSMState
	.globl _StopSWTimer
	.globl _StartSWTimer
	.globl _TestPressKeyAfterTimeout
	.globl _AddPressKeyCnt
	.globl _ClearPressKeyCnt
	.globl _GetKeyState
	.globl _RST_GetFlagStatus
	.globl _IWDG_ReloadCounter
	.globl _RecoverLedState
	.globl _DoClickAction
	.globl _EnterStudyMode
	.globl _ExitStudyMode
	.globl _ClearIDTable
	.globl _KeyCallBack
	.globl _TimerCallBack
	.globl _SMLearnProcess
	.globl _SMNormalProcess
	.globl _SMPowerOnProcess
	.globl _CheckCtrPriority
;--------------------------------------------------------
; ram data
;--------------------------------------------------------
	.area DATA
_SMPowerOnLearnProcess_poweronlearnID_65536_576:
	.ds 4
__ram_power_flag	=	0x01ff
;--------------------------------------------------------
; ram data
;--------------------------------------------------------
	.area INITIALIZED
_gTest:
	.ds 1
_gPowerOnLearn:
	.ds 6
_gTimerFlashLedCnt:
	.ds 1
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
	clrw	x
	ldw	_SMPowerOnLearnProcess_poweronlearnID_65536_576+2, x
	ldw	_SMPowerOnLearnProcess_poweronlearnID_65536_576+0, x
;--------------------------------------------------------
; Home
;--------------------------------------------------------
	.area HOME
	.area HOME
;--------------------------------------------------------
; code
;--------------------------------------------------------
	.area CODE
;	-----------------------------------------
;	 function FactoryTest
;	-----------------------------------------
_FactoryTest:
	ld	a, _gTest+0
	cp	a, #0x05
	jrne	00118$
	ret
00118$:
	mov	_gTest+0, #0x01
	push	#0x04
	push	#0x00
	call	_CheckCtrPriority
	popw	x
	tnz	a
	jrne	00120$
	ret
00120$:
	push	#0xe8
	push	#0x03
	push	#0x00
	push	#0x01
	push	#0x00
	push	#0x01
	push	#0x00
	push	#0x64
	push	#0x00
	push	#0x00
	call	_SetLEDStatus
	addw	sp, #10
	ret
;	-----------------------------------------
;	 function SetLedsState
;	-----------------------------------------
_SetLedsState:
	ld	a, (0x06, sp)
	push	a
	ld	a, (0x04, sp)
	push	a
	call	_CheckCtrPriority
	popw	x
	tnz	a
	jrne	00110$
	ret
00110$:
	ldw	x, (0x04, sp)
	pushw	x
	ld	a, (0x05, sp)
	push	a
	call	_SetLED
	addw	sp, #3
	ret
;	-----------------------------------------
;	 function SetRelayAndLedState
;	-----------------------------------------
_SetRelayAndLedState:
	ldw	x, (0x04, sp)
	pushw	x
	ld	a, (0x05, sp)
	push	a
	call	_RelayControl
	addw	sp, #3
	ld	a, (0x03, sp)
	push	a
	call	_GetRelayStatus
	pop	a
	ld	a, (0x06, sp)
	push	a
	pushw	x
	ld	a, (0x06, sp)
	push	a
	call	_SetLedsState
	addw	sp, #4
	ld	a, (0x03, sp)
	push	a
	call	_GetRelayStatus
	pop	a
	cpw	x, #0x03e8
	jrne	00102$
	push	#0xe0
	push	#0x93
	push	#0x04
	push	#0x00
	push	#0x06
	call	_StartSWTimer
	addw	sp, #5
	ret
00102$:
	push	#0x06
	call	_StopSWTimer
	pop	a
	ret
;	-----------------------------------------
;	 function RecoverLedState
;	-----------------------------------------
_RecoverLedState:
	push	#0x00
	call	_GetRelayStatus
	pop	a
	push	#0x01
	pushw	x
	push	#0x00
	call	_SetLedsState
	addw	sp, #4
	ret
;	-----------------------------------------
;	 function DoClickAction
;	-----------------------------------------
_DoClickAction:
	push	#0x01
	ldw	x, (0x05, sp)
	pushw	x
	ld	a, (0x06, sp)
	push	a
	call	_SetRelayAndLedState
	addw	sp, #4
	ret
;	-----------------------------------------
;	 function EnterStudyMode
;	-----------------------------------------
_EnterStudyMode:
	clr	_gTimerFlashLedCnt+0
	push	#0x02
	clrw	x
	pushw	x
	ld	a, (0x06, sp)
	push	a
	call	_SetRelayAndLedState
	addw	sp, #4
	ret
;	-----------------------------------------
;	 function ExitStudyMode
;	-----------------------------------------
_ExitStudyMode:
	clr	_gTimerFlashLedCnt+0
	push	#0x02
	clrw	x
	pushw	x
	push	#0x00
	call	_SetRelayAndLedState
	addw	sp, #4
	clr	a
	ret
;	-----------------------------------------
;	 function ClearIDTable
;	-----------------------------------------
_ClearIDTable:
	ld	a, (0x03, sp)
	cp	a, #0x01
	jrnc	00102$
	ld	a, (0x03, sp)
	push	a
	call	_ResetIDTable
	pop	a
	ld	a, #0x01
	ret
00102$:
	clr	a
	ret
;	-----------------------------------------
;	 function KeyCallBack
;	-----------------------------------------
_KeyCallBack:
	tnz	(0x04, sp)
	jrne	00118$
	call	_GetSMState
	cp	a, #0x03
	jrne	00108$
	ld	a, (0x05, sp)
	cp	a, #0x02
	jreq	00168$
	ret
00168$:
	push	#0x02
	push	#0x03
	push	#0x04
	push	#0x01
	call	_ExitMode
	addw	sp, #4
	tnz	a
	jrne	00169$
	ret
00169$:
	jp	_ExitStudyMode
00108$:
	call	_GetSMState
	cp	a, #0x05
	jrne	00171$
	ret
00171$:
	push	#0x82
	push	#0xff
	push	#0xff
	ld	a, (0x06, sp)
	push	a
	call	_DoClickAction
	addw	sp, #4
	ret
00118$:
	ld	a, (0x05, sp)
	cp	a, #0x04
	jrne	00115$
	push	#0x03
	push	#0x02
	push	#0x04
	push	#0x01
	call	_StartMode
	addw	sp, #4
	tnz	a
	jrne	00176$
	ret
00176$:
	ld	a, (0x03, sp)
	push	a
	call	_EnterStudyMode
	pop	a
	ret
00115$:
	ld	a, (0x05, sp)
	cp	a, #0x0c
	jreq	00179$
	ret
00179$:
	push	#0x05
	push	#0x03
	push	#0x04
	push	#0x01
	call	_ExitMode
	addw	sp, #4
	push	#0x05
	push	#0x05
	push	#0x03
	push	#0x00
	call	_StartMode
	addw	sp, #4
	push	#0x00
	call	_ResetIDTable
	pop	a
	ret
;	-----------------------------------------
;	 function TimerCallBack
;	-----------------------------------------
_TimerCallBack:
	push	a
	ld	a, #0x01
	ld	(0x01, sp), a
	ld	a, (0x04, sp)
	cp	a, #0x07
	jrule	00168$
	jp	00124$
00168$:
	clrw	x
	ld	a, (0x04, sp)
	ld	xl, a
	sllw	x
	ldw	x, (#00169$, x)
	jp	(x)
00169$:
	.dw	#00101$
	.dw	#00105$
	.dw	#00112$
	.dw	#00115$
	.dw	#00118$
	.dw	#00121$
	.dw	#00122$
	.dw	#00123$
00101$:
	call	_GetSMState
	cp	a, #0x04
	jreq	00102$
	call	_GetSMState
	cp	a, #0x05
	jreq	00175$
	jp	00125$
00175$:
00102$:
	push	#0x02
	push	#0xff
	push	#0xff
	push	#0x00
	call	_SetRelayAndLedState
	addw	sp, #4
	jp	00125$
00105$:
	call	_GetSMState
	cp	a, #0x03
	jreq	00178$
	jp	00125$
00178$:
	inc	_gTimerFlashLedCnt+0
	ld	a, _gTimerFlashLedCnt+0
	cp	a, #0x14
	jrule	00179$
	ld	a, #0x01
	.byte 0x21
00179$:
	clr	a
00180$:
	tnz	a
	jreq	00106$
	tnz	a
	jrne	00182$
	jp	00125$
00182$:
	btjt	_gTimerFlashLedCnt+0, #0, 00184$
	jp	00125$
00184$:
00106$:
	push	#0x02
	push	#0xff
	push	#0xff
	push	#0x00
	call	_SetRelayAndLedState
	addw	sp, #4
	jra	00125$
00112$:
	push	#0x02
	push	#0x04
	push	#0x02
	push	#0x00
	call	_ExitMode
	addw	sp, #4
	tnz	a
	jreq	00125$
	call	_ExitStudyMode
	jra	00125$
00115$:
	push	#0x02
	push	#0x05
	push	#0x03
	push	#0x00
	call	_ExitMode
	addw	sp, #4
	tnz	a
	jreq	00125$
	call	_ExitStudyMode
	jra	00125$
00118$:
	push	#0x02
	push	#0x03
	push	#0x04
	push	#0x01
	call	_ExitMode
	addw	sp, #4
	tnz	a
	jreq	00125$
	call	_ExitStudyMode
	jra	00125$
00121$:
	mov	_gPowerOnLearn+0, #0x01
	push	#0x05
	call	_StopSWTimer
	pop	a
	jra	00125$
00122$:
	push	#0x01
	clrw	x
	pushw	x
	push	#0x00
	call	_SetRelayAndLedState
	addw	sp, #4
	push	#0x06
	call	_StopSWTimer
	pop	a
	jra	00125$
00123$:
	mov	_gTest+0, #0x05
	push	#0x07
	call	_StopSWTimer
	pop	a
	call	_RecoverLedState
	.byte 0xc5
00124$:
	clr	(0x01, sp)
00125$:
	ld	a, (0x01, sp)
	addw	sp, #1
	ret
;	-----------------------------------------
;	 function SMPowerOnLearnProcess
;	-----------------------------------------
_SMPowerOnLearnProcess:
	sub	sp, #10
	ldw	y, (0x0d, sp)
	ldw	(0x09, sp), y
	ldw	x, y
	ld	a, (0x7, x)
	cp	a, #0x41
	jrc	00156$
	jp	00114$
00156$:
	ldw	x, (0x09, sp)
	pushw	x
	call	_GetTelRCTimes
	popw	x
	tnz	a
	jreq	00157$
	jp	00114$
00157$:
	ldw	x, (0x09, sp)
	ld	a, (0x1, x)
	cp	a, #0x01
	jreq	00113$
	cp	a, #0x02
	jreq	00163$
	jp	00114$
00163$:
00113$:
	ldw	x, (0x0d, sp)
	pushw	x
	call	_GetTelFrameKeyState
	popw	x
	dec	a
	jrne	00114$
	ldw	x, sp
	incw	x
	pushw	x
	ldw	x, sp
	addw	x, #5
	pushw	x
	ldw	x, (0x11, sp)
	pushw	x
	push	#0x00
	call	_SearchTelfromTable
	addw	sp, #7
	tnz	a
	jrne	00105$
	push	#0x01
	push	#0x00
	call	_CheckCtrPriority
	popw	x
	tnz	a
	jreq	00105$
	push	#0x00
	call	_GetRelayStatus
	pop	a
	tnzw	x
	jrne	00105$
	push	#0xe8
	push	#0x03
	push	#0x00
	push	#0x01
	push	#0x00
	push	#0x01
	push	#0x00
	push	#0x64
	push	#0x00
	push	#0x00
	call	_SetLEDStatus
	addw	sp, #10
00105$:
	ldw	x, (0x09, sp)
	ldw	y, x
	ldw	y, (0x5, y)
	ldw	x, (0x3, x)
	ldw	(0x07, sp), x
	cpw	y, _SMPowerOnLearnProcess_poweronlearnID_65536_576+2
	jrne	00171$
	ldw	x, (0x07, sp)
	cpw	x, _SMPowerOnLearnProcess_poweronlearnID_65536_576+0
	jreq	00107$
00171$:
	ldw	_SMPowerOnLearnProcess_poweronlearnID_65536_576+2, y
	ldw	x, (0x07, sp)
	ldw	_SMPowerOnLearnProcess_poweronlearnID_65536_576+0, x
	push	#<(_gPowerOnLearn+1)
	push	#((_gPowerOnLearn+1) >> 8)
	call	_ClearPressKeyCnt
	popw	x
00107$:
	push	#0xd0
	push	#0x07
	clrw	x
	pushw	x
	push	#<(_gPowerOnLearn+1)
	push	#((_gPowerOnLearn+1) >> 8)
	call	_AddPressKeyCnt
	addw	sp, #6
00114$:
	addw	sp, #10
	ret
;	-----------------------------------------
;	 function SMLearnProcess
;	-----------------------------------------
_SMLearnProcess:
	sub	sp, #10
	ldw	x, (0x0d, sp)
	jrne	00157$
	jp	00115$
00157$:
	ldw	y, (0x0d, sp)
	ldw	(0x09, sp), y
	ldw	x, sp
	incw	x
	pushw	x
	ldw	x, (0x0b, sp)
	pushw	x
	call	_TransTeltoSingleIdEntry
	addw	sp, #4
	tnz	a
	jreq	00115$
	push	#0x00
	call	_GetKeyState
	addw	sp, #1
	tnz	a
	jrne	00108$
	ld	a, _gTimerFlashLedCnt+0
	cp	a, #0x14
	jrugt	00103$
00108$:
	push	#0x00
	call	_GetKeyState
	addw	sp, #1
	ldw	x, (0x09, sp)
	addw	x, #0x0007
	tnz	a
	jrne	00111$
	ld	a, _gTimerFlashLedCnt+0
	cp	a, #0x14
	jrugt	00111$
	ld	a, (x)
	cp	a, #0x41
	jrc	00103$
00111$:
	pushw	x
	push	#0x00
	call	_GetKeyState
	addw	sp, #1
	popw	x
	tnz	a
	jreq	00115$
	ld	a, (x)
	cp	a, #0x35
	jrnc	00115$
	ldw	x, (0x09, sp)
	pushw	x
	call	_GetTelRCTimes
	popw	x
	tnz	a
	jrne	00115$
00103$:
	ldw	y, (0x05, sp)
	ldw	(0x09, sp), y
	ldw	y, (0x03, sp)
	ldw	(0x07, sp), y
	ldw	x, (0x09, sp)
	jrne	00102$
	ldw	x, (0x07, sp)
	jreq	00115$
00102$:
	ldw	x, sp
	incw	x
	pushw	x
	push	#0x00
	call	_AddIdEntryOverlay
	addw	sp, #3
	push	#0x02
	push	#0x03
	push	#0x04
	push	#0x01
	call	_ExitMode
	addw	sp, #4
	call	_ExitStudyMode
00115$:
	addw	sp, #10
	ret
;	-----------------------------------------
;	 function SMNormalProcess
;	-----------------------------------------
_SMNormalProcess:
	sub	sp, #6
	ld	a, _gPowerOnLearn+0
	jrne	00104$
	ldw	x, #(_gPowerOnLearn+0)+1
	push	#0xd0
	push	#0x07
	push	#0x00
	push	#0x00
	push	#0x03
	pushw	x
	call	_TestPressKeyAfterTimeout
	addw	sp, #7
	tnz	a
	jreq	00104$
	mov	_gPowerOnLearn+0, #0x01
	push	#0x03
	push	#0x02
	push	#0x04
	push	#0x01
	call	_StartMode
	addw	sp, #4
	tnz	a
	jreq	00104$
	push	#0x00
	call	_EnterStudyMode
	pop	a
00104$:
	ldw	x, (0x09, sp)
	jreq	00119$
	ldw	y, (0x09, sp)
	ldw	(0x01, sp), y
	ldw	x, y
	ld	a, (0x6, x)
	ld	(0x06, sp), a
	ld	a, (0x5, x)
	ldw	x, (0x3, x)
	tnz	(0x06, sp)
	jrne	00128$
	tnz	a
	jrne	00128$
	tnzw	x
	jrne	00128$
	ldw	x, y
	ld	a, (0x7, x)
	cp	a, #0x35
	jrnc	00119$
	ld	a, (0x2, y)
	cp	a, #0x20
	jrne	00119$
	addw	sp, #6
	jp	_FactoryTest
00128$:
	clr	a
00117$:
	push	a
	pushw	y
	clrw	x
	pushw	x
	ldw	x, (0x06, sp)
	pushw	x
	push	a
	call	_DoSMNormalPassiveProcess
	addw	sp, #5
	popw	y
	pop	a
	inc	a
	cp	a, #0x01
	jrc	00117$
	ld	a, _gPowerOnLearn+0
	dec	a
	jreq	00119$
	pushw	y
	call	_SMPowerOnLearnProcess
	popw	x
00119$:
	addw	sp, #6
	ret
;	-----------------------------------------
;	 function SMPowerOnProcess
;	-----------------------------------------
_SMPowerOnProcess:
	push	#0x02
	push	#0x01
	push	#0x08
	push	#0x08
	call	_StartMode
	addw	sp, #4
	push	#0x10
	call	_RST_GetFlagStatus
	addw	sp, #1
	tnz	a
	jreq	00130$
	ret
00130$:
	push	#0x04
	call	_RST_GetFlagStatus
	addw	sp, #1
	tnz	a
	jreq	00131$
	ret
00131$:
	push	#0x02
	call	_RST_GetFlagStatus
	addw	sp, #1
	tnz	a
	jreq	00132$
	ret
00132$:
	push	#0x01
	call	_RST_GetFlagStatus
	addw	sp, #1
	tnz	a
	jreq	00102$
	ret
00102$:
	ld	a, __ram_power_flag+0
	cp	a, #0xa5
	jrne	00107$
	ret
	jra	00107$
	ret
00107$:
	clr	_gTest+0
	mov	_gPowerOnLearn+0, #0x00
	push	#0x30
	push	#0x75
	clrw	x
	pushw	x
	push	#0x07
	call	_StartSWTimer
	addw	sp, #5
	push	#0x88
	push	#0x13
	clrw	x
	pushw	x
	push	#0x05
	call	_StartSWTimer
	addw	sp, #5
	mov	__ram_power_flag+0, #0xa5
	jp	_IWDG_ReloadCounter
;	-----------------------------------------
;	 function CheckCtrPriority
;	-----------------------------------------
_CheckCtrPriority:
	ld	a, (0x04, sp)
	cp	a, #0x00
	jreq	00102$
	ld	a, (0x04, sp)
	dec	a
	jreq	00102$
	ld	a, (0x04, sp)
	cp	a, #0x02
	jreq	00106$
	ld	a, (0x04, sp)
	cp	a, #0x04
	jreq	00111$
	ld	a, (0x04, sp)
	cp	a, #0x05
	jreq	00111$
	jra	00112$
00102$:
	call	_GetSMState
	cp	a, #0x03
	jrc	00106$
	call	_GetSMState
	cp	a, #0x05
	jrule	00112$
00106$:
	tnz	_gTest+0
	jreq	00111$
	ld	a, _gTest+0
	cp	a, #0x05
	jrc	00112$
00111$:
	ld	a, #0x01
	ret
00112$:
	clr	a
	ret
	.area CODE
	.area CONST
	.area INITIALIZER
__xinit__gTest:
	.db #0x05	; 5
__xinit__gPowerOnLearn:
	.db #0x01	; 1
	.db #0x00	; 0
	.byte #0x00, #0x00, #0x00, #0x00	; 0
__xinit__gTimerFlashLedCnt:
	.db #0x00	; 0
	.area CABS (ABS)
