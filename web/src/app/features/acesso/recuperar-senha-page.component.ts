import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * "Esqueci minha senha". O envio do link por e-mail não está no MVP: quem
 * esqueceu pede à operadora, que redefine a senha no Atlas Studio (a pessoa
 * troca no primeiro acesso). O link aparece sempre no login do framework.
 */
@Component({
  selector: 'app-recuperar-senha-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="min-vh-100 d-flex align-items-center justify-content-center p-3">
      <div class="card shadow-sm" style="max-width: 440px">
        <div class="card-body p-4 text-center">
          <i class="bi bi-key fs-1 text-primary"></i>
          <h1 class="h5 mt-3">Esqueceu a senha?</h1>
          <p class="text-secondary mb-4">
            Peça à equipe da Mídia Indoor para redefinir o seu acesso. Você recebe
            uma senha temporária e cria a nova no primeiro login.
          </p>
          <a routerLink="/login" class="btn btn-primary">Voltar ao login</a>
        </div>
      </div>
    </div>
  `,
})
export class RecuperarSenhaPageComponent {}
