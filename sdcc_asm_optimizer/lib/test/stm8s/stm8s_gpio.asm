;--------------------------------------------------------
; File Created by SDCC : free open source ANSI-C Compiler
; Version 4.2.0 #13081 (MINGW64)
;--------------------------------------------------------
	.module stm8s_gpio
	.optsdcc -mstm8
	
;--------------------------------------------------------
; Public variables in this module
;--------------------------------------------------------
	.globl _GPIO_DeInit
	.globl _GPIO_Init
	.globl _GPIO_Write
	.globl _GPIO_WriteHigh
	.globl _GPIO_WriteLow
	.globl _GPIO_WriteReverse
	.globl _GPIO_ReadOutputData
	.globl _GPIO_ReadInputData
	.globl _GPIO_ReadInputPin
	.globl _GPIO_ExternalPullUpConfig
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
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 47: void GPIO_DeInit(GPIO_TypeDef* GPIOx)
;	-----------------------------------------
;	 function GPIO_DeInit
;	-----------------------------------------
_GPIO_DeInit:
	exgw	x, y
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 49: GPIOx->ODR = GPIO_ODR_RESET_VALUE; /* Reset Output Data Register */
	clr	(y)
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 50: GPIOx->DDR = GPIO_DDR_RESET_VALUE; /* Reset Data Direction Register */
	ldw	x, y
	incw	x
	incw	x
	clr	(x)
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 51: GPIOx->CR1 = GPIO_CR1_RESET_VALUE; /* Reset Control Register 1 */
	ldw	x, y
	clr	(0x0003, x)
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 52: GPIOx->CR2 = GPIO_CR2_RESET_VALUE; /* Reset Control Register 2 */
	ldw	x, y
	clr	(0x0004, x)
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 53: }
	ret
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 65: void GPIO_Init(GPIO_TypeDef* GPIOx, GPIO_Pin_TypeDef GPIO_Pin, GPIO_Mode_TypeDef GPIO_Mode)
;	-----------------------------------------
;	 function GPIO_Init
;	-----------------------------------------
_GPIO_Init:
	sub	sp, #6
	exgw	x, y
	ld	(0x06, sp), a
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 75: GPIOx->CR2 &= (uint8_t)(~(GPIO_Pin));
	ldw	x, y
	addw	x, #0x0004
	ldw	(0x01, sp), x
	ld	a, (x)
	push	a
	ld	a, (0x07, sp)
	cpl	a
	ld	(0x04, sp), a
	pop	a
	and	a, (0x03, sp)
	ldw	x, (0x01, sp)
	ld	(x), a
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 92: GPIOx->DDR |= (uint8_t)GPIO_Pin;
	ldw	x, y
	incw	x
	incw	x
	ldw	(0x04, sp), x
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 81: if ((((uint8_t)(GPIO_Mode)) & (uint8_t)0x80) != (uint8_t)0x00) /* Output mode */
	tnz	(0x09, sp)
	jrpl	00105$
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 85: GPIOx->ODR |= (uint8_t)GPIO_Pin;
	ld	a, (y)
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 83: if ((((uint8_t)(GPIO_Mode)) & (uint8_t)0x10) != (uint8_t)0x00) /* High level */
	push	a
	ld	a, (0x0a, sp)
	bcp	a, #0x10
	pop	a
	jreq	00102$
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 85: GPIOx->ODR |= (uint8_t)GPIO_Pin;
	or	a, (0x06, sp)
	ld	(y), a
	jra	00103$
00102$:
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 89: GPIOx->ODR &= (uint8_t)(~(GPIO_Pin));
	and	a, (0x03, sp)
	ld	(y), a
00103$:
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 92: GPIOx->DDR |= (uint8_t)GPIO_Pin;
	ldw	x, (0x04, sp)
	ld	a, (x)
	or	a, (0x06, sp)
	ldw	x, (0x04, sp)
	ld	(x), a
	jra	00106$
00105$:
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 97: GPIOx->DDR &= (uint8_t)(~(GPIO_Pin));
	ldw	x, (0x04, sp)
	ld	a, (x)
	and	a, (0x03, sp)
	ldw	x, (0x04, sp)
	ld	(x), a
00106$:
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 106: GPIOx->CR1 |= (uint8_t)GPIO_Pin;
	ldw	x, y
	addw	x, #0x0003
	ld	a, (x)
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 104: if ((((uint8_t)(GPIO_Mode)) & (uint8_t)0x40) != (uint8_t)0x00) /* Pull-Up or Push-Pull */
	push	a
	ld	a, (0x0a, sp)
	bcp	a, #0x40
	pop	a
	jreq	00108$
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 106: GPIOx->CR1 |= (uint8_t)GPIO_Pin;
	or	a, (0x06, sp)
	ld	(x), a
	jra	00109$
00108$:
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 110: GPIOx->CR1 &= (uint8_t)(~(GPIO_Pin));
	and	a, (0x03, sp)
	ld	(x), a
00109$:
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 75: GPIOx->CR2 &= (uint8_t)(~(GPIO_Pin));
	ldw	x, (0x01, sp)
	ld	a, (x)
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 117: if ((((uint8_t)(GPIO_Mode)) & (uint8_t)0x20) != (uint8_t)0x00) /* Interrupt or Slow slope */
	push	a
	ld	a, (0x0a, sp)
	bcp	a, #0x20
	pop	a
	jreq	00111$
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 119: GPIOx->CR2 |= (uint8_t)GPIO_Pin;
	or	a, (0x06, sp)
	ldw	x, (0x01, sp)
	ld	(x), a
	jra	00113$
00111$:
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 123: GPIOx->CR2 &= (uint8_t)(~(GPIO_Pin));
	and	a, (0x03, sp)
	ldw	x, (0x01, sp)
	ld	(x), a
00113$:
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 125: }
	addw	sp, #6
	popw	x
	pop	a
	jp	(x)
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 135: void GPIO_Write(GPIO_TypeDef* GPIOx, uint8_t PortVal)
;	-----------------------------------------
;	 function GPIO_Write
;	-----------------------------------------
_GPIO_Write:
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 137: GPIOx->ODR = PortVal;
	ld	(x), a
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 138: }
	ret
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 148: void GPIO_WriteHigh(GPIO_TypeDef* GPIOx, GPIO_Pin_TypeDef PortPins)
;	-----------------------------------------
;	 function GPIO_WriteHigh
;	-----------------------------------------
_GPIO_WriteHigh:
	push	a
	ld	(0x01, sp), a
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 150: GPIOx->ODR |= (uint8_t)PortPins;
	ld	a, (x)
	or	a, (0x01, sp)
	ld	(x), a
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 151: }
	pop	a
	ret
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 161: void GPIO_WriteLow(GPIO_TypeDef* GPIOx, GPIO_Pin_TypeDef PortPins)
;	-----------------------------------------
;	 function GPIO_WriteLow
;	-----------------------------------------
_GPIO_WriteLow:
	push	a
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 163: GPIOx->ODR &= (uint8_t)(~PortPins);
	push	a
	ld	a, (x)
	ld	(0x02, sp), a
	pop	a
	cpl	a
	and	a, (0x01, sp)
	ld	(x), a
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 164: }
	pop	a
	ret
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 174: void GPIO_WriteReverse(GPIO_TypeDef* GPIOx, GPIO_Pin_TypeDef PortPins)
;	-----------------------------------------
;	 function GPIO_WriteReverse
;	-----------------------------------------
_GPIO_WriteReverse:
	push	a
	ld	(0x01, sp), a
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 176: GPIOx->ODR ^= (uint8_t)PortPins;
	ld	a, (x)
	xor	a, (0x01, sp)
	ld	(x), a
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 177: }
	pop	a
	ret
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 185: uint8_t GPIO_ReadOutputData(GPIO_TypeDef* GPIOx)
;	-----------------------------------------
;	 function GPIO_ReadOutputData
;	-----------------------------------------
_GPIO_ReadOutputData:
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 187: return ((uint8_t)GPIOx->ODR);
	ld	a, (x)
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 188: }
	ret
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 196: uint8_t GPIO_ReadInputData(GPIO_TypeDef* GPIOx)
;	-----------------------------------------
;	 function GPIO_ReadInputData
;	-----------------------------------------
_GPIO_ReadInputData:
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 198: return ((uint8_t)GPIOx->IDR);
	ld	a, (0x1, x)
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 199: }
	ret
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 207: BitStatus GPIO_ReadInputPin(GPIO_TypeDef* GPIOx, GPIO_Pin_TypeDef GPIO_Pin)
;	-----------------------------------------
;	 function GPIO_ReadInputPin
;	-----------------------------------------
_GPIO_ReadInputPin:
	push	a
	ld	(0x01, sp), a
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 209: return ((BitStatus)(GPIOx->IDR & (uint8_t)GPIO_Pin));
	ld	a, (0x1, x)
	and	a, (0x01, sp)
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 210: }
	addw	sp, #1
	ret
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 219: void GPIO_ExternalPullUpConfig(GPIO_TypeDef* GPIOx, GPIO_Pin_TypeDef GPIO_Pin, FunctionalState NewState)
;	-----------------------------------------
;	 function GPIO_ExternalPullUpConfig
;	-----------------------------------------
_GPIO_ExternalPullUpConfig:
	push	a
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 227: GPIOx->CR1 |= (uint8_t)GPIO_Pin;
	addw	x, #0x0003
	push	a
	ld	a, (x)
	ld	(0x02, sp), a
	pop	a
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 225: if (NewState != DISABLE) /* External Pull-Up Set*/
	tnz	(0x04, sp)
	jreq	00102$
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 227: GPIOx->CR1 |= (uint8_t)GPIO_Pin;
	or	a, (0x01, sp)
	ld	(x), a
	jra	00104$
00102$:
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 230: GPIOx->CR1 &= (uint8_t)(~(GPIO_Pin));
	cpl	a
	and	a, (0x01, sp)
	ld	(x), a
00104$:
;	.\..\..\..\Libraries\STM8S_StdPeriph_Driver\src\stm8s_gpio.c: 232: }
	pop	a
	popw	x
	pop	a
	jp	(x)
	.area CODE
	.area CONST
	.area INITIALIZER
	.area CABS (ABS)
