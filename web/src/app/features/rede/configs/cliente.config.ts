import { ColunaDef, FormItemConfig } from '@atlas/framework';
import { TelefoneFormConfig } from '../../../shared/configs/telefone.config';
import { FUSOS_BRASIL, opcoes } from '../../../shared/models/indoor.util';

export const SITUACAO_CLIENTE_BADGE: Record<string, string> = {
  Ativo: 'bg-success',
  Bloqueado: 'bg-danger',
};

export const CLIENTE_COLUNAS: ColunaDef[] = [
  { campo: 'nome', titulo: 'Cliente', principal: true, tipo: 'texto' },
  { campo: 'nomeResponsavel', titulo: 'Responsável', tipo: 'texto' },
  { campo: 'cidade', titulo: 'Cidade', tipo: 'texto' },
  { campo: 'quantidadeTelas', titulo: 'TVs com anúncio', tipo: 'numero' },
  { campo: 'usoMidiasAtivas', titulo: 'Mídias ativas', tipo: 'texto' },
  { campo: 'usoArmazenamento', titulo: 'Armazenamento', tipo: 'texto' },
  { campo: 'diretorio', titulo: 'Diretório das TVs', tipo: 'texto' },
  { campo: 'situacao', titulo: 'Situação', tipo: 'badge', badgeMap: SITUACAO_CLIENTE_BADGE },
];

/**
 * Espelha CriarClienteCommandValidator / AtualizarClienteCommandValidator.
 * Na edição o e-mail fica travado: é o login do responsável e muda no Studio.
 */
export function criarCamposCliente(edicao: boolean): FormItemConfig[] {
  return [
    { type: 'texto', name: 'nome', label: 'Nome do cliente', required: true, col: 'col-12', placeholder: 'Ex.: Tapiocaria da Praia' },
    { type: 'numero', name: 'limiteMidiasAtivas', label: 'Mídias ativas (plano)', required: true, col: 'col-md-6', value: 1, min: 1, max: 1000,
      hint: edicao
        ? 'Reduzir não inativa nada: só impede ativar novas além do limite.'
        : 'Quantas mídias podem estar ativas ao mesmo tempo.' },
    { type: 'numero', name: 'cotaArmazenamentoMb', label: 'Espaço (MB)', required: true, col: 'col-md-6', value: 200, min: 1, max: 1024,
      hint: 'Soma dos vídeos e imagens.' },
    { type: 'texto', name: 'nomeResponsavel', label: 'Responsável', required: !edicao, col: 'col-md-6' },
    { type: 'texto', name: 'email', label: 'E-mail de acesso', required: true, col: 'col-md-6', disabled: edicao,
      hint: edicao ? 'É o login do responsável: muda na conta do usuário, no Atlas Studio.' : 'O responsável recebe aqui a senha temporária.' },
    TelefoneFormConfig.get({ col: 'col-md-6' }),
    { type: 'texto', name: 'documento', label: 'CPF ou CNPJ', col: 'col-md-6', hint: 'Opcional.' },
    { type: 'texto', name: 'cidade', label: 'Cidade', col: 'col-md-6' },
    { type: 'select', name: 'fusoHorario', label: 'Fuso horário', required: true, col: 'col-md-6', value: 'America/Sao_Paulo',
      options: opcoes(FUSOS_BRASIL), hint: 'O "hoje" do painel do cliente (período de exibição das mídias). O das TVs vem do parceiro.' },
    { type: 'textarea', name: 'observacaoInterna', label: 'Observação interna', col: 'col-12', hint: 'Só a operadora vê.' },
  ];
}

export function criarCamposBloqueio(): FormItemConfig[] {
  return [
    {
      type: 'textarea', name: 'motivo', label: 'Motivo do bloqueio', required: true, col: 'col-12',
      hint: 'A equipe do cliente perde o acesso ao painel e as mídias dele saem das grades das TVs na próxima sincronização.',
    },
  ];
}
