import { CampoOption, ColunaDef, FormItemConfig } from '@atlas/framework';
import { TelefoneFormConfig } from '../../../shared/configs/telefone.config';
import { FUSOS_BRASIL, opcoes } from '../../../shared/models/indoor.util';

export const PARCEIRO_COLUNAS: ColunaDef[] = [
  { campo: 'nome', titulo: 'Parceiro', principal: true, tipo: 'texto' },
  { campo: 'nomeResponsavel', titulo: 'Responsável', tipo: 'texto' },
  { campo: 'cidade', titulo: 'Cidade', tipo: 'texto' },
  { campo: 'quantidadeTelas', titulo: 'Telas no local', tipo: 'numero' },
  { campo: 'anuncia', titulo: 'Também anuncia como', tipo: 'texto' },
  { campo: 'ativo', titulo: 'Situação', tipo: 'status' },
];

/** Espelha CriarParceiroCommandValidator / AtualizarParceiroCommandValidator. */
export function criarCamposParceiro(clientes: CampoOption[]): FormItemConfig[] {
  return [
    { type: 'texto', name: 'nome', label: 'Nome do local', required: true, col: 'col-12', placeholder: 'Ex.: Academia Centro' },
    { type: 'texto', name: 'nomeResponsavel', label: 'Responsável', col: 'col-md-6' },
    TelefoneFormConfig.get({ col: 'col-md-6' }),
    { type: 'texto', name: 'email', label: 'E-mail de contato', col: 'col-md-6', hint: 'Só para contato: o parceiro não acessa o sistema.' },
    { type: 'texto', name: 'documento', label: 'CPF ou CNPJ', col: 'col-md-6', hint: 'Opcional.' },
    { type: 'texto', name: 'endereco', label: 'Endereço', col: 'col-12' },
    { type: 'texto', name: 'cidade', label: 'Cidade', col: 'col-md-6', hint: 'O clima das TVs instaladas aqui. Nome repetido? Use "Cidade - UF".' },
    { type: 'select', name: 'fusoHorario', label: 'Fuso horário', required: true, col: 'col-md-6', value: 'America/Sao_Paulo',
      options: opcoes(FUSOS_BRASIL), hint: 'Define o "hoje" e o dia da semana da grade das TVs daqui.' },
    {
      type: 'select', name: 'clienteId', label: 'Também anuncia como cliente', col: 'col-12', options: clientes,
      hint: 'Se o dono do local também anuncia, cadastre-o em Clientes e escolha aqui: no diretório das TVs daqui ele aparece primeiro.',
    },
    { type: 'textarea', name: 'observacao', label: 'Observação', col: 'col-12', hint: 'Acordo, horário de funcionamento, contato do gerente.' },
  ];
}
