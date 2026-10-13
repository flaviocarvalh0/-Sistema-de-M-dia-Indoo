import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { GlobalLoadingComponent } from '@atlas/framework';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, GlobalLoadingComponent],
  template: `
    <router-outlet />
    <atlas-global-loading></atlas-global-loading>
  `,
})
export class App {
  // Toast: NÃO usamos host externo. O ToastService monta o container via CDK
  // overlay (anexado ao body), onde a blindagem de z-index (2147483647 no
  // cdk-global-overlay-wrapper) garante que o toast fique ACIMA de qualquer modal
  // — sem risco de "stacking context trap" que o host renderizado sob <app-root>
  // sofria (o z-index alto não escapava do contexto de empilhamento do app).
}
