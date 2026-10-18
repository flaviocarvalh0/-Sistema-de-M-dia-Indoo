import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CrudService, FormItemConfig, comErroLocal } from '@atlas/framework';

/**
 * Perfil público do cliente logado: o que aparece no diretório das TVs e o
 * aceite para aparecer (backend: Atlas.Application/Business/Clientes/Perfil).
 */
export interface PerfilPublicoDto {
  nome: string;
  exibirNoDiretorioPublico: boolean;
  diretorioAceitoEm?: string | null;
  categoria?: string | null;
  descricaoPublica?: string | null;
  /** Só dígitos, com o 55. */
  whatsappPublico?: string | null;
  instagramPublico?: string | null;
  sitePublico?: string | null;
  /** Só o dono muda o perfil. */
  podeEditar: boolean;
  version: number;
}

/**
 *  - GET /api/v1/business/cliente/perfil
 *  - PUT /api/v1/business/cliente/perfil → nova versão
 */
@Injectable({ providedIn: 'root' })
export class PerfilPublicoService extends CrudService<PerfilPublicoDto, number> {
  protected readonly resource = 'api/v1/business/cliente/perfil';

  obter(): Observable<PerfilPublicoDto> {
    return this.http.get<PerfilPublicoDto>(this.baseUrl);
  }

  /** Sai de modal com `errosInline: true`: erro no banner do modal. */
  salvar(valor: Partial<PerfilPublicoDto>, version: number): Observable<number> {
    return this.http.put<number>(this.baseUrl, {
      exibirNoDiretorioPublico: !!valor.exibirNoDiretorioPublico,
      categoria: valor.categoria || null,
      descricaoPublica: valor.descricaoPublica || null,
      whatsappPublico: valor.whatsappPublico || null,
      instagramPublico: valor.instagramPublico || null,
      sitePublico: valor.sitePublico || null,
      version,
    }, { context: comErroLocal() });
  }
}

/** "5592999990000" → "(92) 99999-0000", como a pessoa digitou. */
export function telefoneLegivel(digitos?: string | null): string {
  if (!digitos) return '';
  const local = digitos.startsWith('55') && digitos.length >= 12 ? digitos.slice(2) : digitos;
  const ddd = local.slice(0, 2);
  const numero = local.slice(2);
  return numero.length >= 8 ? `(${ddd}) ${numero.slice(0, numero.length - 4)}-${numero.slice(-4)}` : digitos;
}

/** Espelha AtualizarPerfilPublicoCommandValidator. */
export function criarCamposPerfilPublico(): FormItemConfig[] {
  return [
    {
      type: 'checkbox', name: 'exibirNoDiretorioPublico', label: 'Aparecer no diretório das TVs', col: 'col-12', value: false,
      placeholder: 'Aceito que meu nome e os contatos abaixo apareçam na página pública das TVs onde meus anúncios passam',
      hint: 'Quem lê o QR code da TV vê a lista de anunciantes com estes contatos (só estes; os QR codes dos anúncios não entram). Desmarque quando quiser sair; vale em até 1 minuto.',
    },
    { type: 'texto', name: 'categoria', label: 'Categoria', col: 'col-md-6', placeholder: 'Ex.: Padaria, Academia, Ótica' },
    {
      type: 'texto', name: 'whatsappPublico', label: 'WhatsApp', col: 'col-md-6', placeholder: '(92) 99999-0000',
      hint: 'Abre a conversa direto no celular de quem lê.',
    },
    {
      type: 'textarea', name: 'descricaoPublica', label: 'Descrição curta', col: 'col-12',
      placeholder: 'Ex.: Pães e cafés feitos na hora, todos os dias', hint: 'Até 140 caracteres.',
    },
    { type: 'texto', name: 'instagramPublico', label: 'Instagram', col: 'col-md-6', placeholder: '@seunegocio' },
    { type: 'texto', name: 'sitePublico', label: 'Site', col: 'col-md-6', placeholder: 'https://…' },
  ];
}
