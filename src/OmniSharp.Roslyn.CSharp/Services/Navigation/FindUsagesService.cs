using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IlSpy.Analyzer.Extraction;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions;
using OmniSharp.Helpers;
using OmniSharp.Mef;
using OmniSharp.Models;
using OmniSharp.Models.FindUsages;
using OmniSharp.Models.Metadata;
using OmniSharp.Options;
using ISymbol = Microsoft.CodeAnalysis.ISymbol;

namespace OmniSharp.Roslyn.CSharp.Services.Navigation
{
    [OmniSharpHandler(OmniSharpEndpoints.FindUsages, LanguageNames.CSharp)]
    public class FindUsagesService : IRequestHandler<FindUsagesRequest, QuickFixResponse>
    {
        private readonly OmniSharpWorkspace _workspace;
        private readonly ILogger<FindUsagesService> _logger;
        private readonly ExternalSourceServiceFactory _externalSourceServiceFactory;
        private readonly OmniSharpOptions _omniSharpOptions;

        [ImportingConstructor]
        public FindUsagesService(OmniSharpWorkspace workspace, ExternalSourceServiceFactory externalSourceServiceFactory, ILoggerFactory loggerFactory, OmniSharpOptions omniSharpOptions)
        {
            _omniSharpOptions = omniSharpOptions;
            _externalSourceServiceFactory = externalSourceServiceFactory;
            _workspace = workspace;
            _logger = loggerFactory.CreateLogger<FindUsagesService>();
        }

        private IEnumerable<ISymbol> GetUsagesSymbols(INamedTypeSymbol sym)
        {
            var foundAssemblies = new ConcurrentBag<IAssemblySymbol>();
            var foundProjects = new ConcurrentBag<Project>();

            foreach (var project in _workspace.CurrentSolution.Projects)
            {
                SearchForAssemblyReferences(sym, project, foundAssemblies, foundProjects);
            }

            ConcurrentBag<ISymbol> found2 = new ConcurrentBag<ISymbol>();

            var gasv = new GetAllSymbolsVisitor(sym, found2);

            foreach (var foundProject in foundProjects)
            {
                gasv.Visit(foundProject.GetCompilationAsync().GetAwaiter().GetResult().Assembly.GlobalNamespace);
            }

            foreach (var assemblySymbol in foundAssemblies)
            {
                gasv.Visit(assemblySymbol.GlobalNamespace);
            }
            gasv.Visit(sym.ContainingAssembly.GlobalNamespace);

            return found2;
        }

        private static void SearchForAssemblyReferences(ISymbol sym, Project project, ConcurrentBag<IAssemblySymbol> found, ConcurrentBag<Project> foundProjects)
        {
            var alreadySearched = new ConcurrentBag<string>();

            if(project.TryGetCompilation(out var compilation))
            {
                foreach (var metadataReference in project.MetadataReferences)
                {
                    if (compilation.GetAssemblyOrModuleSymbol(metadataReference) is IAssemblySymbol assemblyReference)
                    {
                        if (assemblyReference.MetadataName == sym.ContainingAssembly.MetadataName)
                        {
                            foundProjects.Add(project);
                        }

                        SearchForAssemblyReferences(sym, assemblyReference, found, alreadySearched);
                    }
                }
            }
        }

        private static void SearchForAssemblyReferences(ISymbol sym, IAssemblySymbol assembly, ConcurrentBag<IAssemblySymbol> found, ConcurrentBag<string> alreadySearched)
        {
            foreach (var mod in assembly.Modules)
            {
                foreach (var referencedAssemblySymbol in mod.ReferencedAssemblySymbols)
                {
                    if (!alreadySearched.Contains(referencedAssemblySymbol.MetadataName))
                    {
                        if (referencedAssemblySymbol.MetadataName == sym.ContainingAssembly.MetadataName)
                        {
                            found.Add(assembly);
                        }

                        alreadySearched.Add(referencedAssemblySymbol.MetadataName);
                        SearchForAssemblyReferences(sym, referencedAssemblySymbol, found, alreadySearched);
                    }
                }
            }
        }

        private class GetAllSymbolsVisitor : SymbolVisitor
        {
            private readonly ConcurrentBag<ISymbol> _symbols;
            private readonly INamedTypeSymbol _type;

            public GetAllSymbolsVisitor(INamedTypeSymbol type, ConcurrentBag<ISymbol> symbols)
            {
                _symbols = symbols;
                _type = type;
            }

            public override void VisitNamespace(INamespaceSymbol symbol)
            {
                foreach (var namespaceOrTypeSymbol in symbol.GetMembers())
                {
                    namespaceOrTypeSymbol.Accept(this);
                }
            }

            public override void VisitNamedType(INamedTypeSymbol symbol)
            {
                if (symbol.Interfaces.Any(interfaceType => interfaceType.MetadataName == _type.MetadataName))
                {
                    _symbols.Add(symbol);
                }

                var members = symbol.GetMembers();

                foreach (var member in members)
                {
                    member.Accept(this);
                }
            }

            public override void VisitMethod(IMethodSymbol symbol)
            {
                if (symbol.MethodKind != MethodKind.PropertyGet && symbol.MethodKind != MethodKind.PropertySet)
                {
                    if (symbol.ReturnType.MetadataName == _type.MetadataName)
                    {
                        _symbols.Add(symbol);
                    }

                    INamedTypeSymbol returnType = symbol.ReturnType as INamedTypeSymbol;
                    if (returnType != null)
                    {
                        foreach (var typeArg in returnType.TypeArguments)
                        {
                            if (typeArg.MetadataName == _type.MetadataName)
                            {
                                _symbols.Add(typeArg);
                            }
                        }
                    }

                    foreach (var symbolTypeArgument in symbol.TypeArguments)
                    {
                        if (symbolTypeArgument.MetadataName == _type.MetadataName)
                        {
                            _symbols.Add(symbolTypeArgument);
                        }
                    }

                    foreach (var symbolTypeParameter in symbol.TypeParameters)
                    {
                        if (symbolTypeParameter.MetadataName == _type.MetadataName)
                        {
                            _symbols.Add(symbolTypeParameter);
                        }
                    }

                    foreach (var parameter in symbol.Parameters)
                    {
                        if (parameter.Type.MetadataName == _type.MetadataName)
                        {
                            _symbols.Add(parameter);
                        }
                    }
                }
            }

            public override void VisitProperty(IPropertySymbol symbol)
            {
                if (symbol.Type.Name == _type.Name)
                {
                    _symbols.Add(symbol);
                    return;
                }

                INamedTypeSymbol returnType = symbol.Type as INamedTypeSymbol;
                if (returnType != null)
                {
                    foreach (var typeArg in returnType.TypeArguments)
                    {
                        if (typeArg.MetadataName == _type.MetadataName)
                        {
                            _symbols.Add(symbol);
                            return;
                        }
                    }
                }

                foreach (var symbolParameter in symbol.Parameters)
                {
                    if (symbolParameter.MetadataName == _type.MetadataName)
                    {
                        _symbols.Add(symbol);
                        return;
                    }
                }

                foreach (var typeMember in symbol.Type.GetTypeMembers())
                {
                    if (typeMember.MetadataName == _type.MetadataName)
                    {
                        _symbols.Add(symbol);
                        return;
                    }
                }
            }

            public override void VisitField(IFieldSymbol symbol)
            {
                if (symbol.Type.Name == _type.Name)
                {
                    _symbols.Add(symbol);
                    return;
                }

                INamedTypeSymbol returnType = symbol.Type as INamedTypeSymbol;
                if (returnType != null)
                {
                    foreach (var typeArg in returnType.TypeArguments)
                    {
                        if (typeArg.MetadataName == _type.MetadataName)
                        {
                            _symbols.Add(symbol);
                            return;
                        }
                    }
                }

                foreach (var typeMember in symbol.Type.GetTypeMembers())
                {
                    if (typeMember.MetadataName == _type.MetadataName)
                    {
                        _symbols.Add(symbol);
                        return;
                    }
                }
            }

            public override void VisitLocal(ILocalSymbol symbol)
            {
                if (symbol.Type.Name == _type.Name)
                {
                    _symbols.Add(symbol);
                    return;
                }

                INamedTypeSymbol returnType = symbol.Type as INamedTypeSymbol;
                if (returnType != null)
                {
                    foreach (var typeArg in returnType.TypeArguments)
                    {
                        if (typeArg.MetadataName == _type.MetadataName)
                        {
                            _symbols.Add(symbol);
                            return;
                        }
                    }
                }

                foreach (var typeMember in symbol.Type.GetTypeMembers())
                {
                    if (typeMember.MetadataName == _type.MetadataName)
                    {
                        _symbols.Add(symbol);
                        return;
                    }
                }
            }

            public override void VisitParameter(IParameterSymbol symbol)
            {
                //if (symbol.Type.Name == _type.Name)
                //{
                //    _symbols.Add(symbol);
                //}
            }
        }

        public async Task<QuickFixResponse> Handle(FindUsagesRequest request)
        {
            Document document = null;
            ISymbol symbol = null;
            SourceText sourceText = null;
            string projectOutputPath = null;
            int position = 0;
            if (request.FileName.StartsWith("$metadata"))
            {
                var externalSourceService = _externalSourceServiceFactory.Create(_omniSharpOptions);

                document = externalSourceService.FindDocumentInCache(request.FileName) ??
                           _workspace.GetDocument(request.FileName);

                sourceText = await document.GetTextAsync();
                position = sourceText.GetTextPosition(request);

                symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position, CancellationToken.None);

                projectOutputPath = document.Project.OutputFilePath;
            }
            else
            {
                document = _workspace.GetDocument(request.FileName);
            }

            // To produce complete list of usages for symbols in the document wait until all projects are loaded.
            if (document == null)
            {
                var semanticModel = await document.GetSemanticModelAsync();
                sourceText = await document.GetTextAsync();
                position = sourceText.Lines.GetPosition(new LinePosition(request.Line, request.Column));
                symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, _workspace);
                document = await _workspace.GetDocumentFromFullProjectModelAsync(request.FileName);
            }

            if (document == null)
            {
                _logger.LogWarning($"No document found. File: {request.FileName}.");
                return new QuickFixResponse();
            }

            if (symbol is null)
            {
                _logger.LogWarning($"No symbol found. File: {request.FileName}, Line: {request.Line}, Column: {request.Column}.");
                return new QuickFixResponse();
            }

            var symbolName = symbol.GetSymbolName();
            var found = new List<ISymbol>();
            // Compilation compilation = null;
            // foreach (var project in _workspace.CurrentSolution.Projects)
            // {
            //     compilation = await project.GetCompilationAsync();
            //
            //     symbol = compilation.GetTypeByMetadataName(symbolName);
            // }

            // var dllPath = this.FindDllPath(symbol);

            List<Location> locations = null;
            if (symbol != null)
            {
                //var nts = symbol as INamedTypeSymbol;
                //if (nts != null)
                if(symbol is IMethodSymbol || symbol is INamedTypeSymbol || symbol is IPropertySymbol || symbol is IFieldSymbol || symbol is ILocalSymbol)
                {
                    // var symbolCompilation = document.Project.GetCompilationAsync(CancellationToken.None).GetAwaiter().GetResult();
                    //
                    // var reference = compilation.GetMetadataReference(symbol.ContainingAssembly);
                    //
                    // if (reference == null)
                    // {
                    //     reference = symbolCompilation.References.FirstOrDefault(r =>
                    //         r.Display.Replace(".dll", "") == symbol.ContainingAssembly.MetadataName);
                    // }

                    // var assemblyLocation = (reference as PortableExecutableReference)?.FilePath;

                    var ilspyUsageHandler = new IlSpyUsagesFinder();
                    var metadataName = symbol.GetMetadataName();
                    IEnumerable<IlSpyMetadataSource> ilSpyResult = null;

                    if (symbol is IMethodSymbol || symbol is IPropertySymbol || symbol is IFieldSymbol || symbol is ILocalSymbol)
                    {
                        var method = symbol as IMethodSymbol;
                        var property = symbol as IPropertySymbol;
                        var field = symbol as IFieldSymbol;
                        var local = symbol as ILocalSymbol;
                        if (method != null)
                        {
                            var methodParameterTypes = new List<string>();

                            foreach (var parameter in method.Parameters)
                            {
                                methodParameterTypes.Add(parameter.Type.GetMetadataName());
                            }

                            var methodUsagesFinder = new IlSpyMethodUsagesFinder();
                            ilSpyResult = methodUsagesFinder.Run(projectOutputPath, symbol.ContainingType.GetMetadataName(), symbol.Name,
                                methodParameterTypes, document.Project.Name);
                        }
                        else if (property != null)
                        {
                            var methodUsagesFinder = new IlSpyPropertyUsagesFinder();
                            ilSpyResult = methodUsagesFinder.Run(projectOutputPath, symbol.ContainingType.GetMetadataName(), symbol.Name, document.Project.Name);
                        }

                        else if (field != null)
                        {
                            var methodUsagesFinder = new IlSpyFieldUsagesFinder();
                            ilSpyResult = methodUsagesFinder.Run(projectOutputPath, symbol.ContainingType.GetMetadataName(), symbol.Name,
                                document.Project.Name);
                        }

                        else if (local != null)
                        {
                            var definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, _workspace.CurrentSolution);
                            var usages1 = await SymbolFinder.FindReferencesAsync(definition ?? symbol, document.Project.Solution, ImmutableHashSet.Create(document));

                            var localLocations = usages1.SelectMany(u => u.Locations).Select(l => l.Location).ToList();

                            var localQfs = localLocations.Distinct().Select(l => l.GetQuickFix(_workspace));

                            var ilSpyResult2 = new List<IlSpyMetadataSource>();

                            foreach (var quickFix in localQfs)
                            {
                                var ilSpyMetadataSource = new IlSpyMetadataSource()
                                {
                                    Column = quickFix.Column + 1,
                                    SourceLine = quickFix.Text,
                                    Line = quickFix.Line + 1,
                                    StartColumn = quickFix.Column + 1,
                                    EndColumn = quickFix.EndColumn + 1,
                                    FileName = request.FileName,
                                    ProjectName = document.Project.Name,
                                    AssemblyFilePath = null,
                                    ContainingTypeFullName = symbol.ContainingType.GetMetadataName()
                                };
                                ilSpyResult2.Add(ilSpyMetadataSource);
                            }

                            ilSpyResult = ilSpyResult2;
                        }
                    }
                    else if (symbol is INamedTypeSymbol)
                    {
                        var references = new List<string>();
                        foreach (var reference in document.Project.MetadataReferences)
                        {
                            var image = reference as PortableExecutableReference;
                            if (image != null)
                            {
                                references.Add(image.FilePath);
                            }
                        }

                        ilSpyResult = ilspyUsageHandler.Run(projectOutputPath, metadataName, document.Project.Name, references);
                    }

                    // var ilSpyResult = ilspyUsageHandler.Run(projectOutputPath, metadataName, document.Project.Name);

                    var ilSpyMetadataSources = new List<MetadataSource>();
                    foreach (var ilSpyMetadataSource in ilSpyResult)
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

                    var qFixes = new List<QuickFix>();
                    var metadataSources = new List<MetadataSource>();
                    //var metaUsages = GetUsagesSymbols(nts);

                    // foreach (var implementation in metaUsages)
                    // {
                    //     if (implementation.Locations.First().IsInSource)
                    //     {
                    //         qFixes.Add(implementation, _workspace);
                    //     }
                    //     else
                    //     {
                    //         _omniSharpOptions.RoslynExtensionsOptions.EnableDecompilationSupport = true;
                    //         var externalSourceService = _externalSourceServiceFactory.Create(_omniSharpOptions);
                    //         qFixes.Add(metadataSources, implementation, implementation.Locations.First(),
                    //             _workspace, document, externalSourceService);
                    //     }
                    // }

                    var quickFixResponse = new QuickFixResponse(qFixes);
                    quickFixResponse.MetadataFiles = ilSpyMetadataSources;
                    return quickFixResponse;
                }
            }

            if (locations == null)
            {
                var definition = await SymbolFinder.FindSourceDefinitionAsync(symbol, _workspace.CurrentSolution);
                var usages = request.OnlyThisFile
                    ? await SymbolFinder.FindReferencesAsync(definition ?? symbol, _workspace.CurrentSolution, ImmutableHashSet.Create(document))
                    : await SymbolFinder.FindReferencesAsync(definition ?? symbol, _workspace.CurrentSolution);
                locations = usages.SelectMany(u => u.Locations).Select(l => l.Location).ToList();

                if (!request.ExcludeDefinition)
                {
                    // always skip get/set methods of properties from the list of definition locations.
                    var definitionLocations = usages.Select(u => u.Definition)
                        .Where(def => !(def is IMethodSymbol method && method.AssociatedSymbol is IPropertySymbol))
                        .SelectMany(def => def.Locations)
                        .Where(loc => loc.IsInSource && (!request.OnlyThisFile || loc.SourceTree.FilePath == request.FileName));

                    locations.AddRange(definitionLocations);
                }
            }

            var quickFixes = locations.Distinct().Select(l => l.GetQuickFix(_workspace));

            return new QuickFixResponse(quickFixes.Distinct()
                                            .OrderBy(q => q.FileName)
                                            .ThenBy(q => q.Line)
                                            .ThenBy(q => q.Column));
        }
    }
}
