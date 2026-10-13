# ADR-002: Referência do MediatR.Contracts no Domínio

## Status

Aceito

## Data da decisão original

2026-03-16

## Data de adoção no Atlas

2026-06-28

## Contexto

Os eventos de domínio precisam integrar-se ao mecanismo de publicação e assinatura do MediatR utilizado pela aplicação. Para que sejam publicados diretamente, a classe base dos eventos deve satisfazer a interface de marcação `INotification`.

A Clean Architecture procura manter a camada de Domínio independente de frameworks. É necessário decidir entre adicionar uma dependência mínima de contratos do MediatR ou criar uma abstração local e adaptadores de conversão.

## Decisão

Adicionar a referência ao pacote `MediatR.Contracts` diretamente no projeto de Domínio.

A classe `BaseEvent` implementará `INotification`, permitindo que eventos de domínio sejam publicados pelo MediatR sem mapeamentos ou adaptadores intermediários.

## Justificativa

A alternativa seria definir uma interface local, como `IDomainEvent`, e convertê-la para `INotification` na camada de Aplicação.

Embora essa alternativa preserve o ideal de ausência total de dependências externas no Domínio, ela introduziria uma adaptação entre duas interfaces com a mesma finalidade prática.

O pacote `MediatR.Contracts` contém os contratos necessários, sem carregar a implementação completa do MediatR. O acoplamento é reconhecido, mas considerado pequeno, explícito e localizado.

## Consequências

### Ficou mais fácil

- Publicar eventos de domínio diretamente por meio de `IPublisher` ou `IMediator`;
- Eliminar adaptadores e mapeamentos intermediários;
- Manter a integração entre eventos e handlers simples e rastreável.

### Ficou mais difícil

- O projeto de Domínio passa a possuir uma dependência NuGet externa;
- A independência absoluta do domínio em relação a bibliotecas foi intencionalmente flexibilizada;
- A eventual substituição do MediatR exigirá alteração da classe base dos eventos e dos contratos relacionados.

## Limite da decisão

Tipos concretos de implementação do MediatR não deverão ser introduzidos no Domínio além dos contratos estritamente necessários.

---

## Créditos de origem

Esta decisão foi originalmente documentada em inglês por **Jason Taylor** no projeto *Clean Architecture Solution Template*. O conteúdo foi traduzido, adaptado e adotado pela plataforma Atlas em 2026-06-28.

- Projeto original: [https://github.com/jasontaylordev/CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture)
