using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IlSpy.Analyzer.Extraction;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FindImplementations;
using OmniSharp.Models.Metadata;
using OmniSharp.Options;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.FindImplementations, LanguageNames.CSharp)]
    public class FindImplementationsService : IRequestHandler<FindImplementationsRequest, QuickFixResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly ExternalSourceServiceFactory _externalSourceServiceFactory;
        private readonly OmniSharpOptions _omniSharpOptions;

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
            }
            else
            {
                document = _workspace.GetDocument(request.FileName);
                var semanticModel = await document.GetSemanticModelAsync();
                var sourceText = await document.GetTextAsync();
                var position = sourceText.GetTextPosition(request);

                symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, _workspace);
            }

            var response = new QuickFixResponse();

            // if (document != null && symbol != null)
            // {
                var quickFixes = new List<QuickFix>();
                var metadataSources = new List<MetadataSource>();

                if (symbol.IsInterfaceType() || symbol.IsImplementableMember())
                {
                    // SymbolFinder.FindImplementationsAsync will not include the method overrides
                    var implementations = await SymbolFinder.FindImplementationsAsync(symbol, _workspace.CurrentSolution);
                    foreach (var implementation in implementations)
                    {
                        quickFixes.Add(implementation, _workspace);

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

                if (symbol.IsInterfaceType() || symbol.IsImplementableMember() || symbol.IsOverridable() || symbol.IsAbstract)
                {
                    var symbolName = symbol.GetMetadataName();

                    var distinctImplementations = new List<ISymbol>();
                    if (symbol is IMethodSymbol || symbol is IPropertySymbol)
                    {
                        var method = (IMethodSymbol)symbol;
                        /*foreach (var project in _workspace.CurrentSolution.Projects)
                        {*/
                            // if (symbol != null)
                            // {
                                var parameters = new List<string>();

                                foreach (var methodParameter in method.Parameters)
                                {
                                    var parameterType = methodParameter.Type.GetMetadataName();
                                    parameters.Add(parameterType);
                                }

                                var finder = new IlSpyMethodImplementationFinder();
                                var ilSpyUsages = finder.Run(document.Project.OutputFilePath, symbol.ContainingType.GetMetadataName(), symbol.Name, parameters,  document.Project.Name);

                                var ilSpyMetadataSources = new List<MetadataSource>();

                                foreach (var ilSpyMetadataSource in ilSpyUsages)
                                {
                                    var toAdd = new MetadataSource()
                                    {
                                        AssemblyName = ilSpyMetadataSource.AssemblyName,
                                        FileName = ilSpyMetadataSource.FileName,
                                        Line = ilSpyMetadataSource.Line,
                                        Column = ilSpyMetadataSource.Column,
                                        MemberName = ilSpyMetadataSource.MemberName,
                                        ProjectName = ilSpyMetadataSource.ProjectName,
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

                                    ilSpyMetadataSources.Add(toAdd);
                                }

                                var quickFixResponse = new QuickFixResponse(quickFixes);
                                quickFixResponse.MetadataFiles = ilSpyMetadataSources;

                                // var foundImplementations = GetImplementingSymbolsMethod(project, symbol);

                                // foreach (var foundImplementation in foundImplementations)
                                // {
                                //     distinctImplementations.Add(foundImplementation);
                                // }

                                return quickFixResponse;
                            //}
                        //}
                    }
                    else
                    {
                        // foreach (var project in _workspace.CurrentSolution.Projects)
                        // {
                            //var compilation = await project.GetCompilationAsync();

                            // symbol = compilation.GetTypeByMetadataName(symbolName);
                            // if (symbol != null)
                            // {
                                var finder = new IlSpyBaseTypeUsageFinder();
                                var foundUsages = finder.Run(document.Project.OutputFilePath, symbolName, document.Project.Name);
                                var ilSpyMetadataSources = new List<MetadataSource>();

                                foreach (var ilSpyMetadataSource in foundUsages)
                                {
                                    var toAdd = new MetadataSource()
                                    {
                                        AssemblyName = ilSpyMetadataSource.AssemblyName,
                                        FileName = ilSpyMetadataSource.FileName,
                                        Line = ilSpyMetadataSource.Line,
                                        Column = ilSpyMetadataSource.Column,
                                        MemberName = ilSpyMetadataSource.MemberName,
                                        ProjectName = ilSpyMetadataSource.ProjectName,
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

                                    ilSpyMetadataSources.Add(toAdd);
                                }

                                var quickFixResponse = new QuickFixResponse(quickFixes);
                                quickFixResponse.MetadataFiles = ilSpyMetadataSources;

                                // var foundImplementations = GetImplementingSymbols(project, symbolName);

                                // foreach (var foundImplementation in foundImplementations)
                                // {
                                //     if (!distinctImplementations.Any(i => i.Name == foundImplementation.Name))
                                //     {
                                //         distinctImplementations.Add(foundImplementation);
                                //     }
                                // }

                                foreach (var implementation in distinctImplementations)
                                {
                                    if (implementation.Locations.First().IsInSource)
                                    {
                                        quickFixes.Add(implementation, _workspace);
                                    }
                                }

                                return quickFixResponse;
                            //}
                        //}
                    }

                    // foreach (var implementation in distinctImplementations)
                    // {
                    //     if (implementation.Locations.First().IsInSource)
                    //     {
                    //         quickFixes.Add(implementation, _workspace);
                    //     }
                    //     else
                    //     {
                    //         _omniSharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport = true;
                    //         var externalSourceService = _externalSourceServiceFactory.Create(_omniSharpOptions);
                    //         quickFixes.Add(metadataSources, implementation, implementation.Locations.First(), _workspace, document, externalSourceService);
                    //     }
                    //
                    //
                    //     if (implementation.IsOverridable())
                    //     {
                    //         var overrides = await SymbolFinder.FindOverridesAsync(implementation, _workspace.CurrentSolution);
                    //         quickFixes.AddRange(overrides, _workspace);
                    //     }
                    // }
                }
                // else if (symbol is INamedTypeSymbol namedTypeSymbol)
                // {
                //     // for types also include derived classes
                //     // for other symbols, find overrides and include those
                //     // var symbolName = symbol.GetSymbolName();
                //     // var distinctImplementations = new List<INamedTypeSymbol>();
                //     // foreach (var project in _workspace.CurrentSolution.Projects)
                //     // {
                //     //     var compilation = await project.GetCompilationAsync();
                //     //
                //     //     symbol = compilation.GetTypeByMetadataName(symbolName);
                //     //
                //     //     // if (symbol != null)
                //     //     // {
                //     //     //     //var foundImplementations = GetAbstractImplementingSymbols(project, symbolName);
                //     //     //
                //     //     //     // foreach (var foundImplementation in foundImplementations)
                //     //     //     // {
                //     //     //     //     if (!distinctImplementations.Any(i => i.Name == foundImplementation.Name))
                //     //     //     //     {
                //     //     //     //         distinctImplementations.Add(foundImplementation);
                //     //     //     //     }
                //     //     //     // }
                //     //     // }
                //     // }
                //
                //     // foreach (var implementation in distinctImplementations)
                //     // {
                //     //     if (implementation.Locations.First().IsInSource)
                //     //     {
                //     //         quickFixes.Add(implementation, _workspace);
                //     //     }
                //     //     else
                //     //     {
                //     //         _omniSharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport = true;
                //     //         var externalSourceService = _externalSourceServiceFactory.Create(_omniSharpOptions);
                //     //         quickFixes.Add(metadataSources, implementation, implementation.Locations.First(), _workspace, document, externalSourceService);
                //     //     }
                //     //
                //     //
                //     //     if (implementation.IsOverridable())
                //     //     {
                //     //         var overrides = await SymbolFinder.FindOverridesAsync(implementation, _workspace.CurrentSolution);
                //     //         quickFixes.AddRange(overrides, _workspace);
                //     //     }
                //     // }
                //
                //     //var derivedTypes = await SymbolFinder.FindDerivedClassesAsync(namedTypeSymbol, _workspace.CurrentSolution);
                //     //quickFixes.AddRange(derivedTypes, _workspace);
                // }
                // else if (symbol.IsOverridable())
                // {
                //     var symbolName = symbol.GetSymbolName();
                //     var distinctImplementations = new List<ISymbol>();
                //     foreach (var project in _workspace.CurrentSolution.Projects)
                //     {
                //         var compilation = await project.GetCompilationAsync();
                //
                //         if (symbol != null)
                //         {
                //             // var foundImplementations = GetImplementingSymbolsAbstractMethod(project, (IMethodSymbol) symbol);
                //             //
                //             // foreach (var foundImplementation in foundImplementations)
                //             // {
                //             //     if (!distinctImplementations.Any(i => i.Name == foundImplementation.Name))
                //             //     {
                //             //         distinctImplementations.Add(foundImplementation);
                //             //     }
                //             // }
                //         }
                //     }
                //
                //     // foreach (var implementation in distinctImplementations)
                //     // {
                //     //     if (implementation.Locations.First().IsInSource)
                //     //     {
                //     //         quickFixes.Add(implementation, _workspace);
                //     //     }
                //     //     else
                //     //     {
                //     //         _omniSharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport = true;
                //     //         var externalSourceService = _externalSourceServiceFactory.Create(_omniSharpOptions);
                //     //         quickFixes.Add(metadataSources, implementation, implementation.Locations.First(), _workspace, document, externalSourceService);
                //     //     }
                //     //
                //     //     if (implementation.IsOverridable())
                //     //     {
                //     //         var overrides = await SymbolFinder.FindOverridesAsync(implementation, _workspace.CurrentSolution);
                //     //         quickFixes.AddRange(overrides, _workspace);
                //     //     }
                //     // }
                //
                //     //var overrides = await SymbolFinder.FindOverridesAsync(symbol, _workspace.CurrentSolution);
                //     //quickFixes.AddRange(overrides, _workspace);
                // }

                // also include the original declaration of the symbol
                // if (!symbol.IsAbstract)
                // {
                //     // for partial methods, pick the one with body
                //     if (symbol is IMethodSymbol method && method.PartialImplementationPart != null)
                //     {
                //         quickFixes.Add(method.PartialImplementationPart, _workspace);
                //     }
                //     else
                //     {
                //         quickFixes.Add(symbol, _workspace);
                //     }
                // }

                response = new QuickFixResponse(quickFixes.OrderBy(q => q.FileName)
                                                            .ThenBy(q => q.Line)
                                                            .ThenBy(q => q.Column));
                response.MetadataFiles = metadataSources;
            // }

            return response;
        }
    }
}
