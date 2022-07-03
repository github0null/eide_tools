;--------------------------------------------------------
; File Created by SDCC : free open source ANSI-C Compiler
; Version 4.2.0 #13081 (MINGW64)
;--------------------------------------------------------
	.module main
	.optsdcc -mstm8
	
;--------------------------------------------------------
; Public variables in this module
;--------------------------------------------------------
	.globl _main
	.globl _Version
	.globl _RFWorkStateDetection
	.globl _ReceiveOn
	.globl _InitRadio
	.globl _RunKeyCallBacks
	.globl _SetSMState
	.globl _RunStateMachine
	.globl _LoadIDTablesFromFlash
	.globl _InitMemFlash
	.globl _RunTimerCallBacks
	.globl _InitSWTimerArray
	.globl _InitIWDG
	.globl _RunFlashLEDTask
	.globl _InitTIM4
	.globl _InitTIM2
	.globl _delay_ms
	.globl _CLK_SYSCLKConfig
	.globl _CLK_LSICmd
	.globl _CLK_HSICmd
	.globl _CLK_HSECmd
	.globl _CLK_DeInit
	.globl _IWDG_ReloadCounter
	.globl _GPIO_Init
	.globl _EXTI_SetExtIntSensitivity
	.globl _TrapForDebug
	.globl _ResetMcu
;--------------------------------------------------------
; ram data
;--------------------------------------------------------
	.area DATA
;--------------------------------------------------------
; ram data
;--------------------------------------------------------
	.area INITIALIZED
_arr:
		.ds 128
;--------------------------------------------------------
; Stack segment in internal ram
;--------------------------------------------------------
	.area	SSEG
__start__stack::.ds	1

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
	;int s_GSINIT ; reset
;--------------------------------------------------------
; global & static initialisations
;--------------------------------------------------------
	.area HOME
	.area GSINIT
	.area GSFINAL
	.area GSINIT

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
;	.\..\main.c: 30: _Pragma("optimize=none") void Version(char *  ver)
;	-----------------------------------------
;	 function Version
;	-----------------------------------------
_Version:
;	.\..\main.c: 32: }
	ret
;	.\..\main.c: 35: static void InitCLK() 
;	-----------------------------------------
;	 function InitCLK
;	-----------------------------------------
_InitCLK:
;	.\..\main.c: 37: CLK_DeInit();           // RESET Clock Register
	call	_CLK_DeInit
;	.\..\main.c: 38: CLK_HSICmd(ENABLE);     // Open the internal clock
	ld	a, #0x01
	call	_CLK_HSICmd
;	.\..\main.c: 39: CLK_LSICmd(ENABLE);     // Enable LSI clock
	ld	a, #0x01
	call	_CLK_LSICmd
;	.\..\main.c: 40: CLK_HSECmd(DISABLE);    // Close the external clock
	clr	a
	call	_CLK_HSECmd
;	.\..\main.c: 42: CLK_SYSCLKConfig(CLK_PRESCALER_HSIDIV1); //fmaster=16M/1=16M
	clr	a
	call	_CLK_SYSCLKConfig
;	.\..\main.c: 43: CLK_SYSCLKConfig(CLK_PRESCALER_CPUDIV1); //fcpu=fmaster/1, default=/1   
	ld	a, #0x80
	call	_CLK_SYSCLKConfig
;	.\..\main.c: 52: delay_ms(200);
	ldw	x, #0x00c8
;	.\..\main.c: 53: }
	jp	_delay_ms
;	.\..\main.c: 56: static void InitIO()
;	-----------------------------------------
;	 function InitIO
;	-----------------------------------------
_InitIO:
;	.\..\main.c: 61: GPIO_Init(LED1_PORT,   LED1_PIN,  GPIO_MODE_OUT_PP_LOW_SLOW);
	push	#0xc0
	ld	a, #0x08
	ldw	x, #0x500f
	call	_GPIO_Init
;	.\..\main.c: 81: GPIO_Init(IO_KEY1_PORT,     IO_KEY1_PIN,    GPIO_MODE_IN_PU_NO_IT);   
	push	#0x40
	ld	a, #0x10
	ldw	x, #0x5005
	call	_GPIO_Init
;	.\..\main.c: 91: GPIO_Init(IO_RELAY1_PORT,   IO_RELAY1_PIN,  GPIO_MODE_OUT_PP_LOW_SLOW);
	push	#0xc0
	ld	a, #0x08
	ldw	x, #0x5000
	call	_GPIO_Init
;	.\..\main.c: 117: GPIO_Init(CC11xx_CS_GPIO_PORT, CC11xx_CS_PIN, GPIO_MODE_OUT_PP_HIGH_FAST);
	push	#0xf0
	ld	a, #0x08
	ldw	x, #0x500a
	call	_GPIO_Init
;	.\..\main.c: 119: GPIO_Init(CC11xx_GDO2_GPIO_PORT, CC11xx_GDO2_PIN, GPIO_MODE_IN_FL_NO_IT);   //don't enable the interrupt here.
	push	#0x00
	ld	a, #0x10
	ldw	x, #0x500a
	call	_GPIO_Init
;	.\..\main.c: 120: GPIO_Init(CC11xx_SPI_SCK_GPIO_PORT, CC11xx_SPI_SCK_PIN, GPIO_MODE_OUT_PP_LOW_FAST);
	push	#0xe0
	ld	a, #0x20
	ldw	x, #0x500a
	call	_GPIO_Init
;	.\..\main.c: 121: GPIO_Init(CC11xx_SPI_MOSI_GPIO_PORT, CC11xx_SPI_MOSI_PIN, GPIO_MODE_OUT_PP_HIGH_FAST);
	push	#0xf0
	ld	a, #0x40
	ldw	x, #0x500a
	call	_GPIO_Init
;	.\..\main.c: 122: GPIO_Init(CC11xx_SPI_MISO_GPIO_PORT, CC11xx_SPI_MISO_PIN, GPIO_MODE_IN_FL_NO_IT);
	push	#0x00
	ld	a, #0x80
	ldw	x, #0x500a
	call	_GPIO_Init
;	.\..\main.c: 125: EXTI_SetExtIntSensitivity(EXTI_PORT_GPIOC, EXTI_SENSITIVITY_FALL_ONLY);
	push	#0x02
	ld	a, #0x02
	call	_EXTI_SetExtIntSensitivity
;	.\..\main.c: 136: }
	ret
;	.\..\main.c: 141: static void InitHW(void)
;	-----------------------------------------
;	 function InitHW
;	-----------------------------------------
_InitHW:
;	.\..\main.c: 143: InitCLK();
	call	_InitCLK
;	.\..\main.c: 145: InitIWDG();
	call	_InitIWDG
;	.\..\main.c: 150: InitIO();
	call	_InitIO
;	.\..\main.c: 156: InitTIM2();
	call	_InitTIM2
;	.\..\main.c: 165: InitMemFlash();
	call	_InitMemFlash
;	.\..\main.c: 167: disableInterrupts();    // disable all interrupt EA=0
	sim
;	.\..\main.c: 168: InitTIM4();
	call	_InitTIM4
;	.\..\main.c: 172: InitRadio();
	call	_InitRadio
;	.\..\main.c: 173: disableInterrupts();
	sim
;	.\..\main.c: 178: enableInterrupts();
	rim
;	.\..\main.c: 179: IWDG_ReloadCounter();
;	.\..\main.c: 180: }
	jp	_IWDG_ReloadCounter
;	.\..\main.c: 183: static void InitSW() 
;	-----------------------------------------
;	 function InitSW
;	-----------------------------------------
_InitSW:
;	.\..\main.c: 187: InitSWTimerArray();
	call	_InitSWTimerArray
;	.\..\main.c: 188: IWDG_ReloadCounter();
	call	_IWDG_ReloadCounter
;	.\..\main.c: 193: LoadIDTablesFromFlash(); //the interrupt has been enabled in func 'LoadIDTable'
	call	_LoadIDTablesFromFlash
;	.\..\main.c: 204: SetSMState(SM_POWER_ON);
	ld	a, #0x01
	call	_SetSMState
;	.\..\main.c: 206: IWDG_ReloadCounter();
	call	_IWDG_ReloadCounter
;	.\..\main.c: 208: ReceiveOn();
;	.\..\main.c: 212: }
	jp	_ReceiveOn
;	.\..\main.c: 214: void main(void)
;	-----------------------------------------
;	 function main
;	-----------------------------------------
_main:
;	.\..\main.c: 216: Version(VIRTUAL_APP_VERSION);
	ldw	x, #(___str_0+0)
	call	_Version
;	.\..\main.c: 217: InitHW();
	call	_InitHW
;	.\..\main.c: 218: InitSW();
	call	_InitSW
;	.\..\main.c: 219: while(1)
00102$:
;	.\..\main.c: 221: IWDG_ReloadCounter();
	call	_IWDG_ReloadCounter
;	.\..\main.c: 223: RFWorkStateDetection(); // RF Working state and detection processing
	call	_RFWorkStateDetection
;	.\..\main.c: 225: RunStateMachine();
	call	_RunStateMachine
;	.\..\main.c: 227: RunKeyCallBacks();
	call	_RunKeyCallBacks
;	.\..\main.c: 228: RunTimerCallBacks();
	call	_RunTimerCallBacks
;	.\..\main.c: 229: RunFlashLEDTask();
	call	_RunFlashLEDTask
	jra	00102$
;	.\..\main.c: 231: }
	ret
;	.\..\main.c: 250: void TrapForDebug(void)
;	-----------------------------------------
;	 function TrapForDebug
;	-----------------------------------------
_TrapForDebug:
;	.\..\main.c: 253: }
	ret
;	.\..\main.c: 255: void ResetMcu(void)
;	-----------------------------------------
;	 function ResetMcu
;	-----------------------------------------
_ResetMcu:
;	.\..\main.c: 257: WWDG->CR |= 0x80;   //WDGA=1
	bset	0x50d1, #7
;	.\..\main.c: 258: WWDG->CR &= 0xbf;   //T6��0
	bres	0x50d1, #6
;	.\..\main.c: 259: }
	ret
	.area CODE
	.area CONST
	.area CONST
___str_0:
	.ascii "Ver2.3 \rb\n \nnn asdasd ' asd \" "
	.db 0x00
	.area CODE
	.area INITIALIZER
__xinit__arr:
	.db #0x00	; 0
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.db 0x00
	.area CABS (ABS)
