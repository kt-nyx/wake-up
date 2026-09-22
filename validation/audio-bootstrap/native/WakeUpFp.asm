; Copyright (c) 2026 kt-nyx and contributors.
; Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
; The 512-byte FXSAVE area supplied by native code is 16-byte aligned.
; Restore the complete x87 environment/registers and MXCSR, while retaining
; current XMM values so this ordinary function obeys the Windows x64 ABI.
.code
PUBLIC wakeup_fp_save
PUBLIC wakeup_fp_restore
wakeup_fp_save PROC
    fxsave64 [rcx]
    ret
wakeup_fp_save ENDP
wakeup_fp_restore PROC FRAME
    push rsi
    .pushreg rsi
    push rdi
    .pushreg rdi
    sub rsp, 208h
    .allocstack 208h
    .endprolog
    mov rsi, rcx
    mov rdi, rsp
    mov ecx, 64
    rep movsq
    movdqu [rsp+0A0h], xmm0
    movdqu [rsp+0B0h], xmm1
    movdqu [rsp+0C0h], xmm2
    movdqu [rsp+0D0h], xmm3
    movdqu [rsp+0E0h], xmm4
    movdqu [rsp+0F0h], xmm5
    movdqu [rsp+100h], xmm6
    movdqu [rsp+110h], xmm7
    movdqu [rsp+120h], xmm8
    movdqu [rsp+130h], xmm9
    movdqu [rsp+140h], xmm10
    movdqu [rsp+150h], xmm11
    movdqu [rsp+160h], xmm12
    movdqu [rsp+170h], xmm13
    movdqu [rsp+180h], xmm14
    movdqu [rsp+190h], xmm15
    fxrstor64 [rsp]
    add rsp, 208h
    pop rdi
    pop rsi
    ret
wakeup_fp_restore ENDP
END
