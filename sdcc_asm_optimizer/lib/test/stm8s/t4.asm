;--------------------------------------------------------
; File Created by SDCC : free open source ANSI-C Compiler
; Version 4.2.0 #13081 (MINGW64)
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
	.globl _gTimerFlashLedCnt
	.globl _AAA_TEST_VAR2
	.globl _AAA_TEST_VAR
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
_AAA_TEST_VAR::
	.ds 1
_AAA_TEST_VAR2::
	.ds 1
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
_gTimerFlashLedCnt::
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
;	.\..\src\Process.c: 207: static u32 poweronlearnID = 0;
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
;	.\..\src\Process.c: 25: static void FactoryTest()
;	-----------------------------------------
;	 function FactoryTest
;	-----------------------------------------
_FactoryTest:
;	.\..\src\Process.c: 27: AAA_TEST_VAR.a = 0;
	mov	_AAA_TEST_VAR+0, #0x00
;	.\..\src\Process.c: 29: if (gTest != SM_TEST_END)
	ld	a, _gTest+0
	cp	a, #0x05
	jrne	00118$
	ret
00118$:
;	.\..\src\Process.c: 31: gTest = SM_TEST_433M;
	mov	_gTest+0, #0x01
;	.\..\src\Process.c: 32: if (CheckCtrPriority(ID_GATEWAY_CHS, OUTPUT_PRI_FACTORY))
	push	#0x04
	clr	a
	call	_CheckCtrPriority
	tnz	a
	jrne	00120$
	ret
00120$:
;	.\..\src\Process.c: 34: SetLEDStatus(LED_COLOR_BLUE, 100, 1, 1, 0, OUTPUT_ON);
	push	#0xe8
	push	#0x03
	push	#0x00
	push	#0x01
	push	#0x00
	push	#0x01
	push	#0x00
	ldw	x, #0x0064
	clr	a
	call	_SetLEDStatus
;	.\..\src\Process.c: 37: }
	ret
;	.\..\src\Process.c: 39: static void SetLedsState(u8 ch, OUTPUT_STATE_TYPE ctr, OUTPUT_PRIORITY_TYPE pri)
;	-----------------------------------------
;	 function SetLedsState
;	-----------------------------------------
_SetLedsState:
	push	a
	ld	(0x01, sp), a
;	.\..\src\Process.c: 41: if (CheckCtrPriority(ch, pri))
	pushw	x
	ld	a, (0x06, sp)
	push	a
	ld	a, (0x04, sp)
	call	_CheckCtrPriority
	popw	x
	tnz	a
	jreq	00103$
;	.\..\src\Process.c: 43: SetLED(ch, ctr);
	ld	a, (0x01, sp)
	call	_SetLED
00103$:
;	.\..\src\Process.c: 45: }
	pop	a
	popw	x
	pop	a
	jp	(x)
;	.\..\src\Process.c: 47: void SetRelayAndLedState(u8 ch, OUTPUT_STATE_TYPE ctr, OUTPUT_PRIORITY_TYPE pri)
;	-----------------------------------------
;	 function SetRelayAndLedState
;	-----------------------------------------
_SetRelayAndLedState:
	push	a
;	.\..\src\Process.c: 49: RelayControl(ch, ctr);
	ld	(0x01, sp), a
	call	_RelayControl
;	.\..\src\Process.c: 50: SetLedsState(ch, GetRelayStatus(ch), pri);
	ld	a, (0x01, sp)
	call	_GetRelayStatus
	ld	a, (0x04, sp)
	push	a
	ld	a, (0x02, sp)
	call	_SetLedsState
;	.\..\src\Process.c: 51: if (OUTPUT_ON == GetRelayStatus(ch))
	ld	a, (0x01, sp)
	call	_GetRelayStatus
	cpw	x, #0x03e8
	jrne	00102$
;	.\..\src\Process.c: 53: StartSWTimer(TIMER_RELAY_AUTO_CLOSE, TIMER_FOR_RELAY_AUTO_CLOSE);
	push	#0xe0
	push	#0x93
	push	#0x04
	push	#0x00
	ld	a, #0x06
	call	_StartSWTimer
	jra	00104$
00102$:
;	.\..\src\Process.c: 57: StopSWTimer(TIMER_RELAY_AUTO_CLOSE);
	ld	a, #0x06
	call	_StopSWTimer
00104$:
;	.\..\src\Process.c: 59: }
	pop	a
	popw	x
	pop	a
	jp	(x)
;	.\..\src\Process.c: 61: void RecoverLedState()
;	-----------------------------------------
;	 function RecoverLedState
;	-----------------------------------------
_RecoverLedState:
;	.\..\src\Process.c: 63: SetLedsState(RELAY_CH0, GetRelayStatus(RELAY_CH0), OUTPUT_PRI_CLICK_BT);
	clr	a
	call	_GetRelayStatus
	push	#0x01
	clr	a
	call	_SetLedsState
;	.\..\src\Process.c: 64: }
	ret
;	.\..\src\Process.c: 66: void DoClickAction(u8 ch, OUTPUT_STATE_TYPE ctr, TEL_FARME_CMD_DATA_TYPE type)
;	-----------------------------------------
;	 function DoClickAction
;	-----------------------------------------
_DoClickAction:
;	.\..\src\Process.c: 68: SetRelayAndLedState(ch, ctr, OUTPUT_PRI_CLICK_BT);
	push	#0x01
	call	_SetRelayAndLedState
;	.\..\src\Process.c: 70: }
	popw	x
	pop	a
	jp	(x)
;	.\..\src\Process.c: 72: void EnterStudyMode(u8 ch)
;	-----------------------------------------
;	 function EnterStudyMode
;	-----------------------------------------
_EnterStudyMode:
;	.\..\src\Process.c: 74: gTimerFlashLedCnt = 0;
	clr	_gTimerFlashLedCnt+0
;	.\..\src\Process.c: 75: SetRelayAndLedState(ch, OUTPUT_OFF, OUTPUT_PRI_STUDY);
	push	#0x02
	clrw	x
	call	_SetRelayAndLedState
;	.\..\src\Process.c: 76: }
	ret
;	.\..\src\Process.c: 78: u8 ExitStudyMode()
;	-----------------------------------------
;	 function ExitStudyMode
;	-----------------------------------------
_ExitStudyMode:
;	.\..\src\Process.c: 81: gTimerFlashLedCnt = 0;
	clr	_gTimerFlashLedCnt+0
;	.\..\src\Process.c: 82: SetRelayAndLedState(tmp, OUTPUT_OFF, OUTPUT_PRI_STUDY);
	push	#0x02
	clrw	x
	clr	a
	call	_SetRelayAndLedState
;	.\..\src\Process.c: 83: return tmp;
	clr	a
;	.\..\src\Process.c: 84: }
	ret
;	.\..\src\Process.c: 86: bool ClearIDTable(u8 ch)
;	-----------------------------------------
;	 function ClearIDTable
;	-----------------------------------------
_ClearIDTable:
;	.\..\src\Process.c: 88: if (ch < MAX_NUM_OF_ID_TABLE)
	cp	a, #0x01
	jrnc	00102$
;	.\..\src\Process.c: 90: ResetIDTable(ch);
	call	_ResetIDTable
;	.\..\src\Process.c: 91: return TRUE;
	ld	a, #0x01
	ret
00102$:
;	.\..\src\Process.c: 93: return FALSE;
	clr	a
;	.\..\src\Process.c: 94: }
	ret
;	.\..\src\Process.c: 96: void KeyCallBack(u8 ch, KEY_PRESS_STATE_TYPE pressed_state, KEY_PRESS_TIME_TYPE press_time) 
;	-----------------------------------------
;	 function KeyCallBack
;	-----------------------------------------
_KeyCallBack:
	push	a
	ld	(0x01, sp), a
;	.\..\src\Process.c: 98: if (pressed_state == KEY_RELEASED) // key realse
	tnz	(0x04, sp)
	jrne	00118$
;	.\..\src\Process.c: 100: if (GetSMState() == SM_LEARN)
	call	_GetSMState
	cp	a, #0x03
	jrne	00108$
;	.\..\src\Process.c: 102: if (press_time == KEY_PRESSED_SHORT) // exit study mode
	ld	a, (0x05, sp)
	cp	a, #0x02
	jrne	00120$
;	.\..\src\Process.c: 104: if (ExitMode(TIMER_500MS, TIMER_30S, SM_LEARN, SM_NORMAL))
	push	#0x02
	push	#0x03
	push	#0x04
	ld	a, #0x01
	call	_ExitMode
	tnz	a
	jreq	00120$
;	.\..\src\Process.c: 106: ExitStudyMode();
	call	_ExitStudyMode
	jra	00120$
00108$:
;	.\..\src\Process.c: 112: if (GetSMState() != SM_CLEAR_LEARN)
	call	_GetSMState
	cp	a, #0x05
	jreq	00120$
;	.\..\src\Process.c: 114: DoClickAction(ch, OUTPUT_REV, TEL_FARME_REPORT_EVENT_KEYT);
	push	#0x82
	clrw	x
	decw	x
	ld	a, (0x02, sp)
	call	_DoClickAction
	jra	00120$
00118$:
;	.\..\src\Process.c: 120: if (press_time == KEY_PRESSED_LONG_3S) // study mode
	ld	a, (0x05, sp)
	cp	a, #0x04
	jrne	00115$
;	.\..\src\Process.c: 122: if (StartMode(TIMER_500MS, TIMER_30S, SM_NORMAL, SM_LEARN))
	push	#0x03
	push	#0x02
	push	#0x04
	ld	a, #0x01
	call	_StartMode
	tnz	a
	jreq	00120$
;	.\..\src\Process.c: 124: EnterStudyMode(ch);
	ld	a, (0x01, sp)
	call	_EnterStudyMode
	jra	00120$
00115$:
;	.\..\src\Process.c: 127: else if (press_time == KEY_PRESSED_LONG_10S) //clear study id
	ld	a, (0x05, sp)
	cp	a, #0x0c
	jrne	00120$
;	.\..\src\Process.c: 129: ExitMode(TIMER_500MS, TIMER_30S, SM_LEARN, SM_CLEAR_LEARN);
	push	#0x05
	push	#0x03
	push	#0x04
	ld	a, #0x01
	call	_ExitMode
;	.\..\src\Process.c: 130: StartMode(TIMER_250MS, TIMER_2S, SM_CLEAR_LEARN, SM_CLEAR_LEARN);
	push	#0x05
	push	#0x05
	push	#0x03
	clr	a
	call	_StartMode
;	.\..\src\Process.c: 132: ResetIDTable(0);
	clr	a
	call	_ResetIDTable
00120$:
;	.\..\src\Process.c: 135: }
	ldw	x, (2, sp)
	addw	sp, #5
	jp	(x)
;	.\..\src\Process.c: 137: u8 TimerCallBack(u8 ch)
;	-----------------------------------------
;	 function TimerCallBack
;	-----------------------------------------
_TimerCallBack:
	push	a
;	.\..\src\Process.c: 139: u8 process = 1;
	push	a
	ld	a, #0x01
	ld	(0x02, sp), a
	pop	a
;	.\..\src\Process.c: 140: switch (ch)
	cp	a, #0x07
	jrule	00168$
	jp	00124$
00168$:
	clrw	x
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
;	.\..\src\Process.c: 142: case TIMER_250MS:
00101$:
;	.\..\src\Process.c: 143: if ((GetSMState() == SM_LEARN_FULL) || (GetSMState() ==SM_CLEAR_LEARN))
	call	_GetSMState
	cp	a, #0x04
	jreq	00102$
	call	_GetSMState
	cp	a, #0x05
	jreq	00175$
	jp	00125$
00175$:
00102$:
;	.\..\src\Process.c: 145: SetRelayAndLedState(RELAY_CH0, OUTPUT_REV, OUTPUT_PRI_STUDY);
	push	#0x02
	clrw	x
	decw	x
	clr	a
	call	_SetRelayAndLedState
;	.\..\src\Process.c: 147: break;
	jp	00125$
;	.\..\src\Process.c: 148: case TIMER_500MS:
00105$:
;	.\..\src\Process.c: 149: if (GetSMState() == SM_LEARN)
	call	_GetSMState
	cp	a, #0x03
	jreq	00178$
	jp	00125$
00178$:
;	.\..\src\Process.c: 151: gTimerFlashLedCnt ++;
	inc	_gTimerFlashLedCnt+0
;	.\..\src\Process.c: 152: if ((gTimerFlashLedCnt <=20) || ((gTimerFlashLedCnt > 20) && (gTimerFlashLedCnt%2)))
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
	jreq	00125$
	btjf	_gTimerFlashLedCnt+0, #0, 00125$
00106$:
;	.\..\src\Process.c: 154: SetRelayAndLedState(RELAY_CH0, OUTPUT_REV, OUTPUT_PRI_STUDY);
	push	#0x02
	clrw	x
	decw	x
	clr	a
	call	_SetRelayAndLedState
;	.\..\src\Process.c: 157: break;
	jra	00125$
;	.\..\src\Process.c: 158: case TIMER_1S:
00112$:
;	.\..\src\Process.c: 159: if (ExitMode(TIMER_250MS, TIMER_1S, SM_LEARN_FULL, SM_NORMAL))
	push	#0x02
	push	#0x04
	push	#0x02
	clr	a
	call	_ExitMode
	tnz	a
	jreq	00125$
;	.\..\src\Process.c: 161: ExitStudyMode();
	call	_ExitStudyMode
;	.\..\src\Process.c: 163: break;
	jra	00125$
;	.\..\src\Process.c: 164: case TIMER_2S:
00115$:
;	.\..\src\Process.c: 165: if (ExitMode(TIMER_250MS, TIMER_2S, SM_CLEAR_LEARN, SM_NORMAL))
	push	#0x02
	push	#0x05
	push	#0x03
	clr	a
	call	_ExitMode
	tnz	a
	jreq	00125$
;	.\..\src\Process.c: 167: ExitStudyMode();
	call	_ExitStudyMode
;	.\..\src\Process.c: 169: break;
	jra	00125$
;	.\..\src\Process.c: 170: case TIMER_30S:
00118$:
;	.\..\src\Process.c: 171: if (ExitMode(TIMER_500MS, TIMER_30S, SM_LEARN, SM_NORMAL))
	push	#0x02
	push	#0x03
	push	#0x04
	ld	a, #0x01
	call	_ExitMode
	tnz	a
	jreq	00125$
;	.\..\src\Process.c: 173: ExitStudyMode();
	call	_ExitStudyMode
;	.\..\src\Process.c: 175: break;
	jra	00125$
;	.\..\src\Process.c: 176: case TIMER_REMOTE_LEARN:
00121$:
;	.\..\src\Process.c: 177: gPowerOnLearn.state = SM_POWER_LEARN_END;
	mov	_gPowerOnLearn+0, #0x01
;	.\..\src\Process.c: 178: StopSWTimer(TIMER_REMOTE_LEARN);
	ld	a, #0x05
	call	_StopSWTimer
;	.\..\src\Process.c: 179: break;
	jra	00125$
;	.\..\src\Process.c: 180: case TIMER_RELAY_AUTO_CLOSE:
00122$:
;	.\..\src\Process.c: 181: SetRelayAndLedState(RELAY_CH0, OUTPUT_OFF, OUTPUT_PRI_CLICK_BT);
	push	#0x01
	clrw	x
	clr	a
	call	_SetRelayAndLedState
;	.\..\src\Process.c: 182: StopSWTimer(TIMER_RELAY_AUTO_CLOSE);
	ld	a, #0x06
	call	_StopSWTimer
;	.\..\src\Process.c: 183: break;
	jra	00125$
;	.\..\src\Process.c: 191: case TIMER_FACTORY_TEST:
00123$:
;	.\..\src\Process.c: 192: gTest = SM_TEST_END;
	mov	_gTest+0, #0x05
;	.\..\src\Process.c: 193: StopSWTimer(TIMER_FACTORY_TEST);
	ld	a, #0x07
	call	_StopSWTimer
;	.\..\src\Process.c: 194: RecoverLedState();
	call	_RecoverLedState
;	.\..\src\Process.c: 195: break;
;	.\..\src\Process.c: 196: default:
;	.\..\src\Process.c: 197: process = 0;
;	.\..\src\Process.c: 199: }
	.byte 0xc5
00124$:
	clr	(0x01, sp)
00125$:
;	.\..\src\Process.c: 200: return process;
	ld	a, (0x01, sp)
;	.\..\src\Process.c: 201: }
	addw	sp, #1
	ret
;	.\..\src\Process.c: 203: static void SMPowerOnLearnProcess(const TEL_RPS_1BS_TYPE * ptr_tel)
;	-----------------------------------------
;	 function SMPowerOnLearnProcess
;	-----------------------------------------
_SMPowerOnLearnProcess:
	sub	sp, #12
;	.\..\src\Process.c: 208: if ((ptr_tel->frameRSSI < RSSI_THRESHOLD_POWER_ON_LEARNING) && (0 == GetTelRCTimes(ptr_tel)) && 
	ldw	(0x0b, sp), x
	ld	a, (0x7, x)
	cp	a, #0x41
	jrc	00156$
	jp	00114$
00156$:
	ldw	x, (0x0b, sp)
	call	_GetTelRCTimes
	tnz	a
	jreq	00157$
	jp	00114$
00157$:
;	.\..\src\Process.c: 209: ((NP_SWITCH == ptr_tel->cmdType) || (NP_CIRCLE_SWITCH == ptr_tel->cmdType)) &&
	ldw	x, (0x0b, sp)
	ld	a, (0x1, x)
	cp	a, #0x01
	jreq	00113$
	cp	a, #0x02
	jrne	00114$
00113$:
;	.\..\src\Process.c: 210: (GetTelFrameKeyState(ptr_tel) == KEY_PRESSED))
	ldw	x, (0x0b, sp)
	call	_GetTelFrameKeyState
	dec	a
	jrne	00114$
;	.\..\src\Process.c: 212: if (0 == SearchTelfromTable(0, ptr_tel, index_of_identry, &out))  // id not learn, will flash led once time
	ldw	x, sp
	incw	x
	pushw	x
	ldw	x, sp
	addw	x, #5
	pushw	x
	ldw	x, (0x0f, sp)
	clr	a
	call	_SearchTelfromTable
	tnz	a
	jrne	00105$
;	.\..\src\Process.c: 214: if (CheckCtrPriority(LED_COLOR_BLUE, OUTPUT_PRI_CLICK_BT) && (OUTPUT_OFF == GetRelayStatus(RELAY_CH0)))
	push	#0x01
	clr	a
	call	_CheckCtrPriority
	tnz	a
	jreq	00105$
	clr	a
	call	_GetRelayStatus
	tnzw	x
	jrne	00105$
;	.\..\src\Process.c: 216: SetLEDStatus(LED_COLOR_BLUE, 100, 1, 1, 0, OUTPUT_ON);
	push	#0xe8
	push	#0x03
	push	#0x00
	push	#0x01
	push	#0x00
	push	#0x01
	push	#0x00
	ldw	x, #0x0064
	clr	a
	call	_SetLEDStatus
00105$:
;	.\..\src\Process.c: 219: if (poweronlearnID != ptr_tel->chipId)
	ldw	x, (0x0b, sp)
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
;	.\..\src\Process.c: 221: poweronlearnID =  ptr_tel->chipId;
	ldw	_SMPowerOnLearnProcess_poweronlearnID_65536_576+2, y
	ldw	x, (0x07, sp)
	ldw	_SMPowerOnLearnProcess_poweronlearnID_65536_576+0, x
;	.\..\src\Process.c: 222: ClearPressKeyCnt(&gPowerOnLearn.press);
	ldw	x, #(_gPowerOnLearn+1)
	call	_ClearPressKeyCnt
00107$:
;	.\..\src\Process.c: 224: AddPressKeyCnt(&gPowerOnLearn.press, POWERON_LEARN_PRESS_CNTCNT_TIME_LIMIT);
	push	#0xd0
	push	#0x07
	clrw	x
	pushw	x
	ldw	x, #(_gPowerOnLearn+1)
	call	_AddPressKeyCnt
00114$:
;	.\..\src\Process.c: 226: }
	addw	sp, #12
	ret
;	.\..\src\Process.c: 228: void SMLearnProcess(TEL_GENERAL_MSG_TYPE * ptr_tel)
;	-----------------------------------------
;	 function SMLearnProcess
;	-----------------------------------------
_SMLearnProcess:
	sub	sp, #8
;	.\..\src\Process.c: 231: if (ptr_tel && TransTeltoSingleIdEntry(&(ptr_tel->tel), &id_entry_temp))
	ldw	(0x07, sp), x
	jreq	00115$
	ldw	x, sp
	incw	x
	pushw	x
	ldw	x, (0x09, sp)
	call	_TransTeltoSingleIdEntry
	tnz	a
	jreq	00115$
;	.\..\src\Process.c: 233: if (((KEY_RELEASED == GetKeyState(RELAY_CH0)) && (gTimerFlashLedCnt > 20)) || 
	clr	a
	call	_GetKeyState
	tnz	a
	jrne	00108$
	ld	a, _gTimerFlashLedCnt+0
	cp	a, #0x14
	jrugt	00103$
00108$:
;	.\..\src\Process.c: 234: ((KEY_RELEASED == GetKeyState(RELAY_CH0)) && (gTimerFlashLedCnt <= 20) && 
	clr	a
	call	_GetKeyState
;	.\..\src\Process.c: 235: (ptr_tel->tel.frameRSSI < RSSI_THRESHOLD_POWER_ON_LEARNING)) || 
	ldw	x, (0x07, sp)
	addw	x, #0x0007
;	.\..\src\Process.c: 234: ((KEY_RELEASED == GetKeyState(RELAY_CH0)) && (gTimerFlashLedCnt <= 20) && 
	tnz	a
	jrne	00111$
	ld	a, _gTimerFlashLedCnt+0
	cp	a, #0x14
	jrugt	00111$
;	.\..\src\Process.c: 235: (ptr_tel->tel.frameRSSI < RSSI_THRESHOLD_POWER_ON_LEARNING)) || 
	ld	a, (x)
	cp	a, #0x41
	jrc	00103$
00111$:
;	.\..\src\Process.c: 236: (GetKeyState(RELAY_CH0) && (ptr_tel->tel.frameRSSI < RSSI_PRESSKEY_LEARN) && 
	pushw	x
	clr	a
	call	_GetKeyState
	popw	x
	tnz	a
	jreq	00115$
	ld	a, (x)
	cp	a, #0x35
	jrnc	00115$
;	.\..\src\Process.c: 237: (0 == GetTelRCTimes(&ptr_tel->tel)))) ////RSSI is Strong & no restansmit
	ldw	x, (0x07, sp)
	call	_GetTelRCTimes
	tnz	a
	jrne	00115$
00103$:
;	.\..\src\Process.c: 239: if (FACTORYTEST_EMPTY_CHIPID == id_entry_temp.chipId)
	ldw	x, (0x05, sp)
	ldw	y, (0x03, sp)
	tnzw	x
	jrne	00102$
	tnzw	y
	jreq	00115$
;	.\..\src\Process.c: 241: return ;
00102$:
;	.\..\src\Process.c: 243: AddIdEntryOverlay(RELAY_CH0, &id_entry_temp);
	ldw	x, sp
	incw	x
	clr	a
	call	_AddIdEntryOverlay
;	.\..\src\Process.c: 244: ExitMode(TIMER_500MS, TIMER_30S, SM_LEARN, SM_NORMAL);
	push	#0x02
	push	#0x03
	push	#0x04
	ld	a, #0x01
	call	_ExitMode
;	.\..\src\Process.c: 245: ExitStudyMode();
	call	_ExitStudyMode
00115$:
;	.\..\src\Process.c: 248: }
	addw	sp, #8
	ret
;	.\..\src\Process.c: 250: void SMNormalProcess(TEL_GENERAL_MSG_TYPE * ptr_tel)
;	-----------------------------------------
;	 function SMNormalProcess
;	-----------------------------------------
_SMNormalProcess:
	pushw	x
	ldw	(0x01, sp), x
;	.\..\src\Process.c: 253: if ((SM_POWER_LEARN_START == gPowerOnLearn.state) && 
	ld	a, _gPowerOnLearn+0
	jrne	00104$
;	.\..\src\Process.c: 254: TestPressKeyAfterTimeout(&gPowerOnLearn.press, POWERON_LEARN_PRESS_CNT, POWERON_LEARN_PRESS_CNTCNT_TIME_LIMIT))
	push	#0xd0
	push	#0x07
	clrw	x
	pushw	x
	ld	a, #0x03
	ldw	x, #(_gPowerOnLearn+1)
	call	_TestPressKeyAfterTimeout
	tnz	a
	jreq	00104$
;	.\..\src\Process.c: 256: gPowerOnLearn.state = SM_POWER_LEARN_END;
	mov	_gPowerOnLearn+0, #0x01
;	.\..\src\Process.c: 257: if (StartMode(TIMER_500MS, TIMER_30S, SM_NORMAL, SM_LEARN))
	push	#0x03
	push	#0x02
	push	#0x04
	ld	a, #0x01
	call	_StartMode
	tnz	a
	jreq	00104$
;	.\..\src\Process.c: 259: EnterStudyMode(0);
	clr	a
	call	_EnterStudyMode
00104$:
;	.\..\src\Process.c: 262: if (NULL == ptr_tel)
;	.\..\src\Process.c: 264: return;
;	.\..\src\Process.c: 266: if (FACTORYTEST_EMPTY_CHIPID == ptr_tel->tel.chipId)
	ldw	x, (0x01, sp)
	jreq	00119$
	ldw	y, x
	ldw	y, (0x5, y)
	ldw	x, (0x3, x)
	tnzw	y
	jrne	00128$
	tnzw	x
	jrne	00128$
;	.\..\src\Process.c: 268: if((ptr_tel->tel.frameRSSI < RSSI_THRESHOLD_FAC_TEST) && (ptr_tel->tel.cmdData == IDENTRY_CONTACT_MARK_NONE))
	ldw	x, (0x01, sp)
	ld	a, (0x7, x)
	cp	a, #0x35
	jrnc	00119$
	ldw	x, (0x01, sp)
	ld	a, (0x2, x)
	cp	a, #0x20
	jrne	00119$
;	.\..\src\Process.c: 270: FactoryTest();
	popw	x
	jp	_FactoryTest
;	.\..\src\Process.c: 275: for (i=0; i < MAX_NUM_OF_ID_TABLE; i++)
00128$:
	clr	a
00117$:
;	.\..\src\Process.c: 277: DoSMNormalPassiveProcess(i, &(ptr_tel->tel), NULL);
	push	a
	clrw	x
	pushw	x
	ldw	x, (0x04, sp)
	call	_DoSMNormalPassiveProcess
	pop	a
;	.\..\src\Process.c: 275: for (i=0; i < MAX_NUM_OF_ID_TABLE; i++)
	inc	a
	cp	a, #0x01
	jrc	00117$
;	.\..\src\Process.c: 279: if (gPowerOnLearn.state != SM_POWER_LEARN_END)
	ld	a, _gPowerOnLearn+0
	dec	a
	jreq	00119$
;	.\..\src\Process.c: 281: SMPowerOnLearnProcess(&(ptr_tel->tel));
	ldw	x, (0x01, sp)
	popw	x
	jp	_SMPowerOnLearnProcess
00119$:
;	.\..\src\Process.c: 284: }
	popw	x
	ret
;	.\..\src\Process.c: 292: void SMPowerOnProcess()
;	-----------------------------------------
;	 function SMPowerOnProcess
;	-----------------------------------------
_SMPowerOnProcess:
;	.\..\src\Process.c: 295: StartMode(MAX_NUM_OF_TIMER, MAX_NUM_OF_TIMER, SM_POWER_ON, SM_NORMAL);
	push	#0x02
	push	#0x01
	push	#0x08
	ld	a, #0x08
	call	_StartMode
;	.\..\src\Process.c: 308: if ((RST_GetFlagStatus(RST_FLAG_EMCF) != RESET) || (RST_GetFlagStatus(RST_FLAG_ILLOPF) != RESET) 
	ld	a, #0x10
	call	_RST_GetFlagStatus
	tnz	a
	jreq	00130$
	ret
00130$:
	ld	a, #0x04
	call	_RST_GetFlagStatus
	tnz	a
	jreq	00131$
	ret
00131$:
;	.\..\src\Process.c: 309: ||(RST_GetFlagStatus(RST_FLAG_IWDGF) != RESET) || (RST_GetFlagStatus(RST_FLAG_WWDGF) != RESET))
	ld	a, #0x02
	call	_RST_GetFlagStatus
	tnz	a
	jreq	00132$
	ret
00132$:
	ld	a, #0x01
	call	_RST_GetFlagStatus
	tnz	a
	jreq	00102$
;	.\..\src\Process.c: 311: return;
	ret
00102$:
;	.\..\src\Process.c: 314: if(_ram_power_flag==NRST_BOOT_FLAG_VAL) //NRT reboot, ram data will keep
	ld	a, __ram_power_flag+0
	cp	a, #0xa5
	jrne	00107$
	ret
	jra	00107$
;	.\..\src\Process.c: 316: return;
	ret
00107$:
;	.\..\src\Process.c: 318: gTest = SM_TEST_START;
	clr	_gTest+0
;	.\..\src\Process.c: 319: gPowerOnLearn.state = SM_POWER_LEARN_START;
	mov	_gPowerOnLearn+0, #0x00
;	.\..\src\Process.c: 320: StartSWTimer(TIMER_FACTORY_TEST, FACTORYTEST_POWERON_TIME_LIMIT);
	push	#0x30
	push	#0x75
	clrw	x
	pushw	x
	ld	a, #0x07
	call	_StartSWTimer
;	.\..\src\Process.c: 321: StartSWTimer(TIMER_REMOTE_LEARN, TIMER_FOR_POWER_ON_LEARN);
	push	#0x88
	push	#0x13
	clrw	x
	pushw	x
	ld	a, #0x05
	call	_StartSWTimer
;	.\..\src\Process.c: 324: _ram_power_flag = NRST_BOOT_FLAG_VAL;
	mov	__ram_power_flag+0, #0xa5
;	.\..\src\Process.c: 325: IWDG_ReloadCounter();
;	.\..\src\Process.c: 326: }
	jp	_IWDG_ReloadCounter
;	.\..\src\Process.c: 328: bool CheckCtrPriority(u8 ch, OUTPUT_PRIORITY_TYPE pri)
;	-----------------------------------------
;	 function CheckCtrPriority
;	-----------------------------------------
_CheckCtrPriority:
;	.\..\src\Process.c: 330: switch (pri)
	ld	a, (0x03, sp)
	cp	a, #0x00
	jreq	00102$
	ld	a, (0x03, sp)
	dec	a
	jreq	00102$
	ld	a, (0x03, sp)
	cp	a, #0x02
	jreq	00106$
	ld	a, (0x03, sp)
	cp	a, #0x04
	jreq	00111$
	ld	a, (0x03, sp)
	cp	a, #0x05
	jreq	00111$
	jra	00112$
;	.\..\src\Process.c: 333: case OUTPUT_PRI_CLICK_BT:
00102$:
;	.\..\src\Process.c: 334: if ((GetSMState()>=SM_LEARN) && (GetSMState()<=SM_CLEAR_LEARN))
	call	_GetSMState
	cp	a, #0x03
	jrc	00106$
	call	_GetSMState
	cp	a, #0x05
	jrule	00112$
;	.\..\src\Process.c: 338: case OUTPUT_PRI_STUDY:
00106$:
;	.\..\src\Process.c: 339: if ((gTest > SM_TEST_START) && (gTest < SM_TEST_END))
	ld	a, _gTest+0
	jreq	00111$
	ld	a, _gTest+0
	cp	a, #0x05
	jrc	00112$
;	.\..\src\Process.c: 344: case OUTPUT_PRI_HIGHEST:
00111$:
;	.\..\src\Process.c: 345: return TRUE;
	ld	a, #0x01
;	.\..\src\Process.c: 346: }
;	.\..\src\Process.c: 347: return FALSE;
	.byte 0x21
00112$:
	clr	a
00113$:
;	.\..\src\Process.c: 348: }
	popw	x
	addw	sp, #1
	jp	(x)
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
	.byte (_AAA_TEST_VAR2 + 0)
	.area CABS (ABS)
