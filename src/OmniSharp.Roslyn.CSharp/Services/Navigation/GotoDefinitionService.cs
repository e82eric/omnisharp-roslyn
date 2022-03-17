#nullable enable

using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using IlSpy.Analyzer.Extraction;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions;
using OmniSharp.Mef;
using OmniSharp.Models.GotoDefinition;
using OmniSharp.Models.Metadata;
using OmniSharp.Options;
using ISymbol = Microsoft.CodeAnalysis.ISymbol;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.GotoDefinition, LanguageNames.CSharp)]
    public class GotoDefinitionService : IRequestHandler<GotoDefinitionRequest, GotoDefinitionResponse>
    {
        private readonly OmniSharpOptions _omnisharpOptions;
        private readonly OmniSharpWorkspace _workspace;
        private readonly ExternalSourceServiceFactory _externalSourceServiceFactory;

        [ImportingConstructor]
        public GotoDefinitionService(OmniSharpWorkspace workspace, ExternalSourceServiceFactory externalSourceServiceFactory, OmniSharpOptions omnisharpOptions)
        {
            _workspace = workspace;
            _externalSourceServiceFactory = externalSourceServiceFactory;
            _omnisharpOptions = omnisharpOptions;
        }

        public async Task<GotoDefinitionResponse> Handle(GotoDefinitionRequest request)
        {
            var externalSourceService = _externalSourceServiceFactory.Create(_omnisharpOptions);
            var cancellationToken = _externalSourceServiceFactory.CreateCancellationToken(_omnisharpOptions, request.Timeout);
            var document = externalSourceService.FindDocumentInCache(request.FileName) ??
                _workspace.GetDocument(request.FileName);

            ISymbol? symbol;
            // try
            // {
                symbol = await GoToDefinitionHelpers.GetDefinitionSymbol(document, request.Line, request.Column, cancellationToken);
//             }
// #pragma warning disable CS0168
//             catch (Exception e)
// #pragma warning restore CS0168
//             {
//                 var semanticModel = await document.GetSemanticModelAsync();
//                 var sourceText = await document.GetTextAsync();
//                 var position = sourceText.GetTextPosition(request);
//
// #pragma warning disable CS8604 // Possible null reference argument.
//                 symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, _workspace);
// #pragma warning restore CS8604 // Possible null reference argument.
//
//             }

            if (symbol == null)
            {
                return new GotoDefinitionResponse();
            }

            var location = symbol.Locations.First();

            GotoDefinitionResponse? response = null;
            if (location.IsInSource && !request.FileName.StartsWith("$metadata"))
            {
                var lineSpan = symbol.Locations.First().GetMappedLineSpan();
                response = new GotoDefinitionResponse
                {
                    FileName = lineSpan.Path,
                    Line = lineSpan.StartLinePosition.Line,
                    Column = lineSpan.StartLinePosition.Character,
                    SourceGeneratedInfo = GoToDefinitionHelpers.GetSourceGeneratedFileInfo(_workspace, location)
                };
            }
            else if (location.IsInMetadata && request.WantMetadata || request.FileName.StartsWith("$metadata"))
            {
                var ilSpyFinder = new IlSpyTypeFinder();
                var projectOutputFilePath = document.Project.OutputFilePath;

                if (projectOutputFilePath == null)
                {
                    projectOutputFilePath = _workspace.CurrentSolution.Projects.Where(p => p.OutputFilePath != null)
                        .FirstOrDefault()?.OutputFilePath;
                }

                var method = symbol as IMethodSymbol;
                var property = symbol as IPropertySymbol;
                if (method != null)
                {
                    var ilSpyMemberFinder = new IlSpyMemberFinder();

                    var methodParameterTypes = new List<string>();

                    if (method.ReducedFrom != null)
                    {
                        foreach (var parameter in method.ReducedFrom.Parameters)
                        {
                            methodParameterTypes.Add(parameter.Type.GetMetadataName());
                        }
                    }
                    else
                    {
                        foreach (var parameter in method.Parameters)
                        {
                            methodParameterTypes.Add(parameter.Type.GetMetadataName());
                        }
                    }

                    var metadataSource = ilSpyMemberFinder.Run(projectOutputFilePath, method.ContainingType.GetSymbolName(), method.Name, methodParameterTypes, document.Project.Name);

                    var add = new MetadataSource()
                    {
                        AssemblyName = metadataSource.AssemblyName,
                        FileName = metadataSource.FileName,
                        Line = metadataSource.Line,
                        Column = metadataSource.Column,
                        MemberName = metadataSource.MemberName,
                        ProjectName = document.Project.Name,
                        SourceLine = metadataSource.SourceLine,
                        SourceText = metadataSource.SourceText,
                        TypeName = metadataSource.TypeName,
                        StatementLine = metadataSource.StatementLine,
                        StartColumn = metadataSource.StartColumn,
                        EndColumn = metadataSource.EndColumn,
                        Token = metadataSource.Token,
                        ContainingTypeToken = metadataSource.ContainingTypeToken,
                        ContainingTypeFullName = metadataSource.ContainingTypeFullName,
                        AssemblyFilePath = metadataSource.AssemblyFilePath
                    };

                    response = new GotoDefinitionResponse
                    {
                        Line = metadataSource.Line,
                        Column = metadataSource.Column,
                        MetadataSource = add,
                    };

                    return response;
                }
                else if (property != null)
                {
                    var ilSpyMemberFinder = new IlSpyPropertyFinder();

                    var metadataSource = ilSpyMemberFinder.Run(projectOutputFilePath, property.ContainingType.GetSymbolName(), property.Name, document.Project.Name);

                    var add = new MetadataSource()
                    {
                        AssemblyName = metadataSource.AssemblyName,
                        FileName = metadataSource.FileName,
                        Line = metadataSource.Line,
                        Column = metadataSource.Column,
                        MemberName = metadataSource.MemberName,
                        ProjectName = document.Project.Name,
                        SourceLine = metadataSource.SourceLine,
                        SourceText = metadataSource.SourceText,
                        TypeName = metadataSource.TypeName,
                        StatementLine = metadataSource.StatementLine,
                        StartColumn = metadataSource.StartColumn,
                        EndColumn = metadataSource.EndColumn,
                        Token = metadataSource.Token,
                        ContainingTypeToken = metadataSource.ContainingTypeToken,
                        ContainingTypeFullName = metadataSource.ContainingTypeFullName,
                        AssemblyFilePath = metadataSource.AssemblyFilePath
                    };

                    response = new GotoDefinitionResponse
                    {
                        Line = metadataSource.Line,
                        Column = metadataSource.Column,
                        MetadataSource = add,
                    };

                    return response;
                }

                var ilSpyMetadataSource = ilSpyFinder.Run(document.Project.Name, projectOutputFilePath, symbol.GetSymbolName());

                var toAdd = new MetadataSource()
                {
                    AssemblyName = ilSpyMetadataSource.AssemblyName,
                    FileName = ilSpyMetadataSource.FileName,
                    Line = ilSpyMetadataSource.Line,
                    Column = ilSpyMetadataSource.Column,
                    MemberName = ilSpyMetadataSource.MemberName,
                    ProjectName = document.Project.Name,
                    SourceLine = ilSpyMetadataSource.SourceLine,
                    SourceText = ilSpyMetadataSource.SourceText,
                    TypeName = ilSpyMetadataSource.TypeName,
                    StatementLine = ilSpyMetadataSource.StatementLine,
                    StartColumn = ilSpyMetadataSource.StartColumn,
                    EndColumn = ilSpyMetadataSource.EndColumn,
                    Token = ilSpyMetadataSource.Token,
                    ContainingTypeToken = ilSpyMetadataSource.ContainingTypeToken,
                    ContainingTypeFullName = ilSpyMetadataSource.ContainingTypeFullName,
                    AssemblyFilePath = ilSpyMetadataSource.AssemblyFilePath
                };

                response = new GotoDefinitionResponse
                {
                    Line = ilSpyMetadataSource.Line,
                    Column = ilSpyMetadataSource.Column,
                    MetadataSource = toAdd,
                };

                return response;
            }

            return response ?? new GotoDefinitionResponse();
        }
    }
}
