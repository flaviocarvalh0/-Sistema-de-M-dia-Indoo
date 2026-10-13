// =========================================================================
// 1. Frameworks, Ecossistema .NET e Bibliotecas Externas (Nuget)
// =========================================================================
global using Ardalis.GuardClauses;
global using AutoMapper;
global using AutoMapper.QueryableExtensions;
global using FluentValidation;
global using MediatR;
global using Microsoft.EntityFrameworkCore;

// =========================================================================
// 2. Namespaces Internos do Ecossistema Atlas (Do Core/Shared para a Aplicação)
// =========================================================================
global using Atlas.Application.Common.Interfaces;
global using Atlas.Application.Common.Models;
global using Atlas.Application.Common.Security;
global using Atlas.Domain.Constants;
global using Atlas.Domain.Platform.Enums;
global using Atlas.Shared.Observability;

// =========================================================================
// 3. Aliases Globais e Resoluções de Ambiguidade
// =========================================================================
global using ValidationException = Atlas.Application.Common.Exceptions.ValidationException;