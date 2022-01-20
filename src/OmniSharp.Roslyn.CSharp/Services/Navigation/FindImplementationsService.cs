using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FindImplementations;
using OmniSharp.Models.Metadata;
using OmniSharp.Options;
using OmniSharp.Roslyn.CSharp.Services.Decompilation;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.FindImplementations, LanguageNames.CSharp)]
    public class FindImplementationsService : IRequestHandler<FindImplementationsRequest, QuickFixResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private ExternalSourceServiceFactory _externalSourceServiceFactory;
        private OmniSharpOptions _omniSharpOptions;

        [ImportingConstructor]
        public FindImplementationsService(OmniSharpWorkspace workspace, ExternalSourceServiceFactory externalSourceServiceFactory, OmniSharpOptions omniSharpOptions)
        {
            _omniSharpOptions = omniSharpOptions;
            _externalSourceServiceFactory = externalSourceServiceFactory;
            _workspace = workspace;
        }

        public async Task<QuickFixResponse> Handle(FindImplementationsRequest request)
        {
            Document document = null;
            ISymbol symbol = null;
            if (request.FileName.StartsWith("$metadata"))
            {
                var externalSourceService = _externalSourceServiceFactory.Create(_omniSharpOptions);

                document = externalSourceService.FindDocumentInCache(request.FileName) ??
                               _workspace.GetDocument(request.FileName);

                var sourceText = await document.GetTextAsync();
                var position = sourceText.GetTextPosition(request);

                symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position, CancellationToken.None);

                var symbolName = symbol.GetSymbolName();

                foreach (var project in _workspace.CurrentSolution.Projects)
                {
                    var compilation = await project.GetCompilationAsync();

                    symbol = compilation.GetTypeByMetadataName(symbolName);
                    if (symbol != null)
                    {
                        var cancellationToken = _externalSourceServiceFactory.CreateCancellationToken(_omniSharpOptions, int.MaxValue);
                        var (metadataDocument, documentPath) = await externalSourceService.GetAndAddExternalSymbolDocument(project, symbol, cancellationToken);

                        if (metadataDocument != null)
                        {
                            document = metadataDocument;
                            break;
                        }
                    }
                }
            }
            else
            {
                document = _workspace.GetDocument(request.FileName);
            }

            var response = new QuickFixResponse();

            if (document != null || symbol !=null)
            {
                var quickFixes = new List<QuickFix>();
                var metadataSources = new List<MetadataSource>();

                if (document != null && symbol == null)
                {
                    var semanticModel = await document.GetSemanticModelAsync();
                    var sourceText = await document.GetTextAsync();
                    var position = sourceText.GetTextPosition(request);

                    symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, _workspace);
                }
                if (symbol == null)
                {
                    return response;
                }

                if (symbol.IsInterfaceType() || symbol.IsImplementableMember())
                {
                    var implementations = await SymbolFinder.FindImplementationsAsync(symbol, _workspace.CurrentSolution);
                    foreach (var implementation in implementations)
                    {
                        if (implementation.Locations.First().IsInSource)
                        {
                            quickFixes.Add(implementation, _workspace);
                        }
                        else
                        {
                            _omniSharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport = true;
                            var externalSourceService = _externalSourceServiceFactory.Create(_omniSharpOptions);
                            quickFixes.Add(metadataSources, implementation, implementation.Locations.First(), _workspace, document, externalSourceService);
                        }
                        

                        if (implementation.IsOverridable())
                        {
                            var overrides = await SymbolFinder.FindOverridesAsync(implementation, _workspace.CurrentSolution);
                            quickFixes.AddRange(overrides, _workspace);
                        }
                    }
                }
                else if (symbol is INamedTypeSymbol namedTypeSymbol)
                {
                    // for types also include derived classes
                    // for other symbols, find overrides and include those
                    var derivedTypes = await SymbolFinder.FindDerivedClassesAsync(namedTypeSymbol, _workspace.CurrentSolution);
                    quickFixes.AddRange(derivedTypes, _workspace);
                }
                else if (symbol.IsOverridable())
                {
                    var overrides = await SymbolFinder.FindOverridesAsync(symbol, _workspace.CurrentSolution);
                    quickFixes.AddRange(overrides, _workspace);
                }

                // also include the original declaration of the symbol
                if (!symbol.IsAbstract)
                {
                    // for partial methods, pick the one with body
                    if (symbol is IMethodSymbol method && method.PartialImplementationPart != null)
                    {
                        quickFixes.Add(method.PartialImplementationPart, _workspace);
                    }
                    else
                    {
                        quickFixes.Add(symbol, _workspace);
                    }
                }

                response = new QuickFixResponse(quickFixes.OrderBy(q => q.FileName)
                                                            .ThenBy(q => q.Line)
                                                            .ThenBy(q => q.Column));
                response.MetadataFiles = metadataSources;
            }

            return response;
        }
    }
}
