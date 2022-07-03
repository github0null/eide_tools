;--------------------------------------------------------
; File Created by SDCC : free open source ANSI-C Compiler
; Version 4.1.0 #12072 (MINGW64)
;--------------------------------------------------------
	.module main
	.optsdcc -mstm8
	
;--------------------------------------------------------
; Public variables in this module
;--------------------------------------------------------
	.globl __this_is_a_example
	.globl _TRAP_IRQHandler
	.globl _main
	.globl _WWDG_SWReset
	.globl _UART1_Cmd
	.globl _TIM4_SelectOnePulseMode
	.globl _TIM4_Cmd
	.globl _TIM4_TimeBaseInit
	.globl _TIM2_Cmd
	.globl _TIM1_Cmd
	.globl _SPI_DeInit
	.globl _IWDG_Enable
	.globl _I2C_AcknowledgeConfig
	.globl _GPIO_WriteReverse
	.globl _GPIO_Init
	.globl _FLASH_DeInit
	.globl _CLK_HSIPrescalerConfig
	.globl _CLK_PeripheralClockConfig
	.globl _BEEP_DeInit
	.globl _arr
	.globl _DelayInit
	.globl _DelayMs
;--------------------------------------------------------
; ram data
;--------------------------------------------------------
	.area DATA
_arr	=	0x0120
_arr2:
	.ds 312
;--------------------------------------------------------
; ram data
;--------------------------------------------------------
	.area INITIALIZED
_f_a:
	.ds 4
;--------------------------------------------------------
; Stack segment in internal ram 
;--------------------------------------------------------
	.area	SSEG
__start__stack:
	.ds	1

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
; interrupt vector 
;--------------------------------------------------------
	.area HOME
__interrupt_vect:
	int s_GSINIT ; reset
	int _TRAP_IRQHandler ; trap
	int 0x000000 ; int0
	int 0x000000 ; int1
	int 0x000000 ; int2
	int __this_is_a_example ; int3
;--------------------------------------------------------
; global & static initialisations
;--------------------------------------------------------
	.area HOME
	.area GSINIT
	.area GSFINAL
	.area GSINIT
__sdcc_init_data:
; stm8_genXINIT() start
	ldw x, #l_DATA
	jreq	00002$
00001$:
	clr (s_DATA - 1, x)
	decw x
	jrne	00001$
00002$:
	ldw	x, #l_INITIALIZER
	jreq	00004$
00003$:
	ld	a, (s_INITIALIZER - 1, x)
	ld	(s_INITIALIZED - 1, x), a
	decw	x
	jrne	00003$
00004$:
; stm8_genXINIT() end
	.area GSFINAL
	jp	__sdcc_program_startup
;--------------------------------------------------------
; Home
;--------------------------------------------------------
	.area HOME
	.area HOME
__sdcc_program_startup:
	jp	_main
;	return from main will return to caller
;--------------------------------------------------------
; code
;--------------------------------------------------------
	.area CODE
;	.\src\main.c: 26: void main(void)
;	-----------------------------------------
;	 function main
;	-----------------------------------------
_main:
	ldw	y, sp
	subw	y, #17
	sub	sp, #255
	sub	sp, #5
;	.\src\main.c: 31: CLK_HSIPrescalerConfig(CLK_PRESCALER_HSIDIV1);
	pushw	y
	push	#0x00
	call	_CLK_HSIPrescalerConfig
	pop	a
	call	_DelayInit
	push	#0x02
	call	_I2C_AcknowledgeConfig
	pop	a
	call	_FLASH_DeInit
	call	_SPI_DeInit
	call	_BEEP_DeInit
	call	_IWDG_Enable
	push	#0x00
	call	_TIM1_Cmd
	pop	a
	push	#0x00
	call	_TIM2_Cmd
	pop	a
	push	#0x00
	call	_TIM4_Cmd
	pop	a
	call	_WWDG_SWReset
	push	#0x01
	call	_UART1_Cmd
	pop	a
	push	#0xe0
	push	#0x20
	push	#0x05
	push	#0x50
	call	_GPIO_Init
	addw	sp, #4
	popw	y
;	.\src\main.c: 60: float ffffa = (arr[12] ^ asdasd[34]) * arr2[2] * f_a;
	ld	a, _arr+12
	ld	(0x11, y), a
	ld	a, (0x23, sp)
	xor	a, (0x11, y)
	ld	xl, a
	ld	a, _arr2+2
	mul	x, a
	pushw	y
	pushw	x
	call	___sint2fs
	addw	sp, #2
	pushw	y
	ldw	y, (3, sp)
	ld	a, (2, sp)
	ld	(0xf, y), a
	ld	a, (1, sp)
	ld	(0xe, y), a
	addw	sp, #4
	pushw	y
	push	_f_a+3
	push	_f_a+2
	push	_f_a+1
	push	_f_a+0
	pushw	x
	ldw	x, y
	ldw	x, (0xe, x)
	pushw	x
	call	___fsmul
	addw	sp, #8
	pushw	y
	ldw	y, (3, sp)
	ld	a, (2, sp)
	ld	(0xf, y), a
	ld	a, (1, sp)
	addw	sp, #4
	ld	(0xe, y), a
;	.\src\main.c: 62: while (ffffa > 12.5f)
	pushw	y
	pushw	x
	ldw	x, y
	ldw	x, (0xe, x)
	pushw	x
	clrw	x
	pushw	x
	push	#0x48
	push	#0x41
	call	___fslt
	addw	sp, #8
	popw	y
	ld	(0x11, y), a
00101$:
	tnz	(0x11, y)
	jreq	00104$
;	.\src\main.c: 64: GPIO_WriteReverse(LED_GPIO_PORT, (GPIO_Pin_TypeDef)LED_GPIO_PINS);
	pushw	y
	push	#0x20
	push	#0x05
	push	#0x50
	call	_GPIO_WriteReverse
	addw	sp, #3
	push	#0xf4
	push	#0x01
	call	_DelayMs
	addw	sp, #2
	popw	y
	jra	00101$
00104$:
;	.\src\main.c: 67: }
	addw	sp, #255
	addw	sp, #5
	ret
;	.\src\main.c: 69: void DelayInit(void)
;	-----------------------------------------
;	 function DelayInit
;	-----------------------------------------
_DelayInit:
;	.\src\main.c: 71: CLK_PeripheralClockConfig(CLK_PERIPHERAL_TIMER4, ENABLE);
	push	#0x01
	push	#0x04
	call	_CLK_PeripheralClockConfig
	addw	sp, #2
;	.\src\main.c: 72: TIM4_TimeBaseInit(TIM4_PRESCALER_64, 249); // 1ms
	push	#0xf9
	push	#0x06
	call	_TIM4_TimeBaseInit
	addw	sp, #2
;	.\src\main.c: 73: TIM4_SelectOnePulseMode(TIM4_OPMODE_SINGLE);
	push	#0x01
	call	_TIM4_SelectOnePulseMode
	pop	a
;	.\src\main.c: 74: }
	ret
;	.\src\main.c: 76: void DelayMs(uint16_t ms)
;	-----------------------------------------
;	 function DelayMs
;	-----------------------------------------
_DelayMs:
	sub	sp, #2
;	.\src\main.c: 78: while (ms--)
	ldw	x, (0x05, sp)
00104$:
	ldw	(0x01, sp), x
	decw	x
	ldw	y, (0x01, sp)
	jreq	00107$
;	.\src\main.c: 80: TIM4->SR1 = (uint8_t)(~TIM4_FLAG_UPDATE);
	mov	0x5344+0, #0xfe
;	.\src\main.c: 81: TIM4->CR1 |= TIM4_CR1_CEN;
	bset	21312, #0
;	.\src\main.c: 82: while ((TIM4->SR1 & (uint8_t)TIM4_FLAG_UPDATE) == 0)
00101$:
	ld	a, 0x5344
	srl	a
	jrc	00104$
	jra	00101$
00107$:
;	.\src\main.c: 85: }
	addw	sp, #2
	ret
;	.\src\main.c: 98: INTERRUPT_HANDLER_TRAP(TRAP_IRQHandler)
;	-----------------------------------------
;	 function TRAP_IRQHandler
;	-----------------------------------------
_TRAP_IRQHandler:
	clr	a
	div	x, a
;	.\src\main.c: 100: while (1)
00102$:
;	.\src\main.c: 102: nop();
	nop
	jra	00102$
;	.\src\main.c: 104: }
	iret
;	.\src\main.c: 111: INTERRUPT_HANDLER(_this_is_a_example, EXTI_PORTA_IRQn)
;	-----------------------------------------
;	 function _this_is_a_example
;	-----------------------------------------
__this_is_a_example:
;	.\src\main.c: 114: }
	iret
	.area CODE
	.area CONST
	.area INITIALIZER
__xinit__f_a:
	.byte #0x40, #0x63, #0xd7, #0x0a	;  3.560000e+000
	.area CABS (ABS)
