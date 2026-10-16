import { Injectable, inject } from '@angular/core';
import { HttpBackend, HttpClient, HttpEvent, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CrudService, comErroLocal } from '@atlas/framework';
import { DistribuicaoVersao, EnvioApk, NovaVersaoApp, VersaoAppDto, VersoesAppDto } from '../models/versao-app.model';

/**
 * Versões do app das TVs (operadora).
 *
 *  - GET    /api/v1/business/rede/app-tv                      → { versoes, telasPareadas, telasSemAtualizacaoAutomatica }
 *  - POST   /api/v1/business/rede/app-tv/envio                → { chave, urlEnvio, expiraEm, tipoConteudo }
 *  - POST   /api/v1/business/rede/app-tv                      → id (confere assinatura, tamanho e SHA-256)
 *  - POST   /api/v1/business/rede/app-tv/{id}/distribuicao?distribuicao=&version= → nova versão
 *  - DELETE /api/v1/business/rede/app-tv/{id}                 (só suspensa)
 */
@Injectable({ providedIn: 'root' })
export class VersaoAppService extends CrudService<VersaoAppDto, number> {
  protected readonly resource = 'api/v1/business/rede/app-tv';

  /** Sem interceptadores: o storage não aceita o token da API. */
  private readonly storage = new HttpClient(inject(HttpBackend));

  listar(): Observable<VersoesAppDto> {
    return this.http.get<VersoesAppDto>(this.baseUrl);
  }

  prepararEnvio(tamanhoBytes: number): Observable<EnvioApk> {
    return this.http.post<EnvioApk>(`${this.baseUrl}/envio`, { tamanhoBytes }, { context: comErroLocal() });
  }

  /** O navegador costuma não dar tipo ao .apk: o Content-Type vai explícito (o bucket só aceita o do APK). */
  enviarArquivo(envio: EnvioApk, arquivo: File): Observable<HttpEvent<unknown>> {
    return this.storage.put(envio.urlEnvio, arquivo, {
      headers: new HttpHeaders({ 'Content-Type': envio.tipoConteudo, 'x-upsert': 'false' }),
      reportProgress: true,
      observe: 'events',
    });
  }

  criar(versao: NovaVersaoApp): Observable<number> {
    return this.http.post<number>(this.baseUrl, versao, { context: comErroLocal() });
  }

  definirDistribuicao(id: number, distribuicao: DistribuicaoVersao, version: number): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/${id}/distribuicao`, null, { params: { distribuicao, version } });
  }

  excluir(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
