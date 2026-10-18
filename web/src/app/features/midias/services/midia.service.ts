import { HttpBackend, HttpClient, HttpEvent, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CrudService, Page, PaginatedResponse, QueryParams, comErroLocal } from '@atlas/framework';
import { EnvioPreparado, LinhaMidia, MidiaDto, NovaMidia, SubstituicaoArquivo, TipoMidia, UsoArmazenamento, SubstituicaoResultado } from '../models/midia.model';
import { paraPage, paramsPaginacao } from '../../../shared/utils/paginacao.util';
import { paraNumero } from '../../../shared/utils/form.util';
import { TIPO_QR_ROTULO } from '../configs/midia.config';
import { formatarBytes, formatarData, formatarDuracao } from '../../../shared/models/indoor.util';

/**
 * Mídias do cliente.
 *
 *  - GET  /api/v1/business/cliente/midias?tipo=&ativo=
 *  - GET  /api/v1/business/cliente/midias/armazenamento
 *  - GET  /api/v1/business/cliente/midias/{id}            (traz urlVisualizacao)
 *  - POST /api/v1/business/cliente/midias/envio            → { chave, urlEnvio, expiraEm }
 *  - POST /api/v1/business/cliente/midias                  → id (depois do PUT no storage)
 *  - PUT  /api/v1/business/cliente/midias/{id}             → nova versão
 *  - PUT  /api/v1/business/cliente/midias/{id}/arquivo     → nova versão (troca o arquivo)
 *  - POST /api/v1/business/cliente/midias/{id}/ativar|inativar?version=
 *  - DELETE /api/v1/business/cliente/midias/{id}
 */
@Injectable({ providedIn: 'root' })
export class MidiaService extends CrudService<MidiaDto, number> {
  protected readonly resource = 'api/v1/business/cliente/midias';

  /**
   * Cliente sem interceptors: o PUT vai direto para o storage (URL assinada),
   * sem cookie de sessão nem tratamento de erro da API.
   */
  private readonly storage = new HttpClient(inject(HttpBackend));

  listar(query: QueryParams, filtros: { tipo?: TipoMidia | null; ativo?: boolean | null } = {}): Observable<Page<LinhaMidia>> {
    return this.http
      .get<PaginatedResponse<MidiaDto>>(this.baseUrl, {
        params: paramsPaginacao(query, {
          tipo: filtros.tipo ?? null,
          ativo: filtros.ativo == null ? null : String(filtros.ativo),
        }),
      })
      .pipe(
        map(resp => paraPage(resp, query)),
        map(page => ({ ...page, content: page.content.map(paraLinha) })),
      );
  }

  usoArmazenamento(): Observable<UsoArmazenamento> {
    return this.http.get<UsoArmazenamento>(`${this.baseUrl}/armazenamento`);
  }

  prepararEnvio(tipoConteudo: string, tamanhoBytes: number): Observable<EnvioPreparado> {
    return this.http.post<EnvioPreparado>(`${this.baseUrl}/envio`, { tipoConteudo, tamanhoBytes }, { context: comErroLocal() });
  }

  /** PUT binário na URL assinada, com eventos de progresso. */
  enviarArquivo(urlEnvio: string, arquivo: File): Observable<HttpEvent<unknown>> {
    return this.storage.put(urlEnvio, arquivo, {
      headers: new HttpHeaders({ 'Content-Type': arquivo.type, 'x-upsert': 'false' }),
      reportProgress: true,
      observe: 'events',
    });
  }

  criar(midia: NovaMidia): Observable<number> {
    return this.http.post<number>(this.baseUrl, midia, { context: comErroLocal() });
  }

  /** Troca o arquivo (já enviado ao storage) de uma mídia existente; a TV baixa de novo pelo hash novo. */
  substituirArquivo(corpo: SubstituicaoArquivo): Observable<SubstituicaoResultado> {
    return this.http.put<SubstituicaoResultado>(`${this.baseUrl}/${corpo.id}/arquivo`, corpo, { context: comErroLocal() });
  }

  /**
   * Sai de modal com `errosInline: true`: erro no banner do modal. O QR code
   * vai junto: sem tipo, a mídia fica sem QR code.
   */
  atualizar(id: number, valor: Partial<MidiaDto> & Record<string, any>, version: number): Observable<number> {
    return this.http.put<number>(`${this.baseUrl}/${id}`, {
      id,
      titulo: valor.titulo,
      duracaoSegundos: Number(valor.duracaoSegundos),
      inicioExibicao: valor.inicioExibicao || null,
      fimExibicao: valor.fimExibicao || null,
      ...corpoQrCode(valor),
      version,
    }, { context: comErroLocal() });
  }

  definirAtivo(id: number, ativo: boolean, version: number): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/${id}/${ativo ? 'ativar' : 'inativar'}`, null, { params: { version } });
  }

  excluir(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}

export const TIPO_MIDIA_ROTULO: Record<TipoMidia, string> = { Video: 'Vídeo', Imagem: 'Imagem' };

/** "Sempre", "A partir de 01/11/2026", "Até 30/11/2026" ou "01/11/2026 a 30/11/2026". */
export function descreverPeriodo(inicio?: string | null, fim?: string | null): string {
  if (inicio && fim) return `${formatarData(inicio)} a ${formatarData(fim)}`;
  if (inicio) return `A partir de ${formatarData(inicio)}`;
  if (fim) return `Até ${formatarData(fim)}`;
  return 'Sempre';
}

/** Os campos do formulário do QR code no formato do AtualizarMidiaCommand. */
export function corpoQrCode(valor: Record<string, any>) {
  const tipo = valor['qrTipo'] || null;
  const destino = {
    Link: valor['qrLink'],
    WhatsApp: valor['qrTelefone'],
    Telefone: valor['qrTelefone'],
    Instagram: valor['qrInstagram'],
    Pix: valor['qrChavePix'],
  }[tipo as string] ?? null;
  const valorPix = paraNumero(valor['qrValor']);
  return {
    qrTipo: tipo,
    qrDestino: tipo ? destino || null : null,
    qrMensagem: tipo === 'WhatsApp' ? valor['qrMensagem'] || null : null,
    qrValor: tipo === 'Pix' && valorPix > 0 ? valorPix : null,
    qrNomeRecebedor: tipo === 'Pix' ? valor['qrNomeRecebedor'] || null : null,
    qrCidadeRecebedor: tipo === 'Pix' ? valor['qrCidadeRecebedor'] || null : null,
    qrLegenda: tipo ? valor['qrLegenda'] || null : null,
    qrPosicao: tipo ? valor['qrPosicao'] || 'InferiorDireito' : null,
    qrTamanho: tipo ? valor['qrTamanho'] || 'Medio' : null,
  };
}

function paraLinha(m: MidiaDto): LinhaMidia {
  return {
    ...m,
    qrCode: m.qrTipo ? TIPO_QR_ROTULO[m.qrTipo] : '—',
    troca: m.trocaPendente ? 'Aguardando aprovação' : m.motivoRecusaTroca ? `Recusada: ${m.motivoRecusaTroca}` : '—',
    tipoRotulo: TIPO_MIDIA_ROTULO[m.tipo] ?? m.tipo,
    tamanho: formatarBytes(m.tamanhoBytes),
    duracao: formatarDuracao(m.duracaoSegundos),
    periodo: descreverPeriodo(m.inicioExibicao, m.fimExibicao),
  };
}
