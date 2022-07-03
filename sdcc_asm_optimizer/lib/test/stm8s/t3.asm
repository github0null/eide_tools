;--------------------------------------------------------
; File Created by SDCC : free open source ANSI-C Compiler
; Version 4.2.0 #13081 (MINGW64)
;--------------------------------------------------------
	.module Hal_IWDG
	.optsdcc -mstm8
	
;--------------------------------------------------------
; Public variables in this module
;--------------------------------------------------------
	.globl _IWDG_Enable
	.globl _IWDG_ReloadCounter
	.globl _IWDG_SetReload
	.globl _IWDG_SetPrescaler
	.globl _IWDG_WriteAccessCmd
	.globl _InitIWDG
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
;	.\..\src\Hal_IWDG.c: 11: void InitIWDG()
;	-----------------------------------------
;	 function InitIWDG
;	-----------------------------------------
_InitIWDG:
;	.\..\src\Hal_IWDG.c: 14: IWDG_Enable();
	call	_IWDG_Enable
;	.\..\src\Hal_IWDG.c: 17: IWDG_WriteAccessCmd(IWDG_WriteAccess_Enable);
	ld	a, #0x55
	call	_IWDG_WriteAccessCmd
;	.\..\src\Hal_IWDG.c: 20: IWDG_SetReload(0xFF);
	ld	a, #0xff
	call	_IWDG_SetReload
;	.\..\src\Hal_IWDG.c: 23: IWDG_SetPrescaler(IWDG_Prescaler_256);
	ld	a, #0x06
	call	_IWDG_SetPrescaler
;	.\..\src\Hal_IWDG.c: 26: IWDG_ReloadCounter(); 
;	.\..\src\Hal_IWDG.c: 27: }
	jp	_IWDG_ReloadCounter
	.area CODE
	.area CONST
	.area INITIALIZER
	.area CABS (ABS)
