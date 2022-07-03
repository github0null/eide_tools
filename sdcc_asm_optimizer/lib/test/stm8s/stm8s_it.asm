;--------------------------------------------------------
; File Created by SDCC : free open source ANSI-C Compiler
; Version 4.2.0 #13081 (MINGW64)
;--------------------------------------------------------
	.module stm8s_it
	.optsdcc -mstm8
	
;--------------------------------------------------------
; Public variables in this module
;--------------------------------------------------------
	.globl _TIM4_UPD_OVF_IRQHandler
	.globl _TIM2_UPD_OVF_BRK_IRQHandler
	.globl _EXTI_PORTC_IRQHandler
	.globl _RunSWTimerArrayTask
	.globl _DetectKeyTask
	.globl _RunLEDTask
	.globl _GetRxDatas2RxFifoBufferArray
	.globl _TIM4_ClearITPendingBit
	.globl _TIM2_ClearITPendingBit
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
;	.\..\stm8s_it.c: 68: INTERRUPT_HANDLER(EXTI_PORTC_IRQHandler, 5)
;	-----------------------------------------
;	 function EXTI_PORTC_IRQHandler
;	-----------------------------------------
_EXTI_PORTC_IRQHandler:
	div	x, a
;	.\..\stm8s_it.c: 70: GetRxDatas2RxFifoBufferArray();
	call	_GetRxDatas2RxFifoBufferArray
;	.\..\stm8s_it.c: 71: }
	iret
;	.\..\stm8s_it.c: 73: INTERRUPT_HANDLER(TIM2_UPD_OVF_BRK_IRQHandler, 13)
;	-----------------------------------------
;	 function TIM2_UPD_OVF_BRK_IRQHandler
;	-----------------------------------------
_TIM2_UPD_OVF_BRK_IRQHandler:
	div	x, a
;	.\..\stm8s_it.c: 75: TIM2_ClearITPendingBit(TIM2_IT_UPDATE);
	ld	a, #0x01
	call	_TIM2_ClearITPendingBit
;	.\..\stm8s_it.c: 76: }
	iret
;	.\..\stm8s_it.c: 78: INTERRUPT_HANDLER(TIM4_UPD_OVF_IRQHandler, 23)
;	-----------------------------------------
;	 function TIM4_UPD_OVF_IRQHandler
;	-----------------------------------------
_TIM4_UPD_OVF_IRQHandler:
	div	x, a
;	.\..\stm8s_it.c: 80: DetectKeyTask();
	call	_DetectKeyTask
;	.\..\stm8s_it.c: 81: RunSWTimerArrayTask();
	call	_RunSWTimerArrayTask
;	.\..\stm8s_it.c: 82: RunLEDTask();
	call	_RunLEDTask
;	.\..\stm8s_it.c: 84: TIM4_ClearITPendingBit(TIM4_IT_UPDATE);
	ld	a, #0x01
	call	_TIM4_ClearITPendingBit
;	.\..\stm8s_it.c: 85: }
	iret
	.area CODE
	.area CONST
	.area INITIALIZER
	.area CABS (ABS)
