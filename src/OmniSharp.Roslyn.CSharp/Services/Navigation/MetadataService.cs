using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.Metadata;
using OmniSharp.Options;
using DecompilationExternalSourceService = OmniSharp.Roslyn.CSharp.Services.Decompilation.DecompilationExternalSourceService;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.Metadata, LanguageNames.CSharp)]
    public class MetadataService : IRequestHandler<MetadataRequest, MetadataResponse>
    {
        private readonly OmniSharpOptions _omniSharpOptions;
        private readonly OmniSharpWorkspace _workspace;
        private readonly ExternalSourceServiceFactory _externalSourceServiceFactory;

        [ImportingConstructor]
        public MetadataService(OmniSharpWorkspace workspace, ExternalSourceServiceFactory externalSourceServiceFactory, OmniSharpOptions omniSharpOptions)
        {
            _workspace = workspace;
            _externalSourceServiceFactory = externalSourceServiceFactory;
            _omniSharpOptions = omniSharpOptions;
            _omniSharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport = true;
        }

        public async Task<MetadataResponse> Handle(MetadataRequest request)
        {
            var externalSourceService = _externalSourceServiceFactory.Create(_omniSharpOptions);

            // if (request.FileName.StartsWith("$metadata"))
            // {
                var result = await Decompile(request, externalSourceService);
                return result;
            // }

            // var response = new MetadataResponse();
            // foreach (var project in _workspace.CurrentSolution.Projects)
            // {
            //     var compilation = await project.GetCompilationAsync();
            //     var symbol = compilation.GetTypeByMetadataName(request.TypeName);
            //     if (symbol == null && !string.IsNullOrEmpty(request.ContainingTypeFullName))
            //     {
            //         symbol = compilation.GetTypeByMetadataName(request.ContainingTypeFullName);
            //     }
            //
            //     if (symbol != null)// && symbol.ContainingAssembly.Name == request.AssemblyName)
            //     {
            //         ISymbol memberSymbol = null;
            //
            //         if (symbol.MetadataName == request.MemberName)
            //         {
            //             memberSymbol = symbol;
            //         }
            //
            //         if (!string.IsNullOrEmpty(request.ContainingTypeFullName))
            //         {
            //             memberSymbol = compilation.GetTypeByMetadataName(request.ContainingTypeFullName);
            //         }
            //
            //         var cancellationToken = _externalSourceServiceFactory.CreateCancellationToken(_omniSharpOptions, request.Timeout);
            //         // Document metadataDocument;
            //         // string documentPath;
            //         // if (request.ContainingTypeToken != 0)
            //         // {
            //         //     (metadataDocument, documentPath) = await ((DecompilationExternalSourceService)externalSourceService).GetAndAddExternalSymbolDocument(
            //         //         project,
            //         //         request.ContainingTypeFullName,
            //         //         request.AssemblyFilePath,
            //         //         request.FileName);
            //         // }
            //         // else
            //         // {
            //         //     (metadataDocument, documentPath) = await externalSourceService.GetAndAddExternalSymbolDocument(project, symbol, cancellationToken);
            //         // }
            //
            //         (metadataDocument, documentPath) = await externalSourceService.GetAndAddExternalSymbolDocument(project, symbol, cancellationToken);
            //
            //         if (metadataDocument != null)
            //         {
            //             var source = await metadataDocument.GetTextAsync();
            //             response.Source = source.ToString();
            //             response.SourceName = documentPath;
            //             if (string.IsNullOrEmpty(request.ContainingTypeFullName))
            //             {
            //                 var metadataLocation = externalSourceService.GetExternalSymbolLocation(memberSymbol, metadataDocument, CancellationToken.None).GetAwaiter().GetResult();
            //                 var mappedSpan = metadataLocation.GetMappedLineSpan();
            //                 response.Line = request.Line != 0 ? request.Line : mappedSpan.StartLinePosition.Line;
            //                 response.Column = request.Column != 0 ? request.Line : mappedSpan.StartLinePosition.Character;
            //             }
            //             else
            //             {
            //                 response.Line = request.StatementLine == 0 ? request.Line : request.StatementLine;
            //                 response.Column = request.StartColumn;
            //                 response.StartColumn = request.StartColumn;
            //                 response.EndColumn = request.EndColumn;
            //                 if (request.StatementLine != 0)
            //                 {
            //                     //response.Line = response.Line + request.StatementLine - 1;
            //                 }
            //             }
            //
            //             return response;
            //         }
            //     }
            // }
            // return response;
        }

        private async Task<MetadataResponse> Decompile(MetadataRequest request, IExternalSourceService externalSourceService)
        {
            MetadataResponse response = new MetadataResponse();
            var project = _workspace.CurrentSolution.Projects.Where(p => p.Name == request.ProjectName).FirstOrDefault();
            (Document metadataDocument, string documentPath) =
                await ((DecompilationExternalSourceService)externalSourceService).GetAndAddExternalSymbolDocument(
                    project,
                    request.ContainingTypeFullName,
                    request.AssemblyFilePath,
                    request.FileName);

            if (metadataDocument != null)
            {
                var source = await metadataDocument.GetTextAsync();

                response.Source = source.ToString();
                response.SourceName = documentPath;

                response.Line = request.StatementLine == 0 ? request.Line : request.StatementLine;
                response.Column = request.StartColumn;
                response.StartColumn = request.StartColumn;
                response.EndColumn = request.EndColumn;
            }

            return response;
        }
    }
}
