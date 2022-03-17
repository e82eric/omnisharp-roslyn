using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Document = Microsoft.CodeAnalysis.Document;

namespace IlSpy.Analyzer.Extraction;

    public class ILSpayCSharpDecompiledSourceService
    {
        public Task<Document> AddSourceToAsync(Document document, string rootFileTypeName, string assemblyLocation)
        {
            // Get the name of the type the symbol is in

            // Decompile
            document = PerformDecompilation(document, rootFileTypeName, assemblyLocation);

            return Task.FromResult(document);
        }

        public Task<Document> AddSourceToAsync(Document document, ISymbol symbol, string assemblyLocation)
        {
            // Get the name of the type the symbol is in
            var containingOrThis = GetContainingTypeOrThis(symbol);;
            var fullName = GetFullReflectionName(containingOrThis);

            // Decompile
            document = PerformDecompilation(document, fullName, assemblyLocation);

            return Task.FromResult(document);
        }

        public Task<Document> AddSourceToAsync(Document document, Compilation symbolCompilation, Microsoft.CodeAnalysis.ISymbol symbol, string assemblyLocation, CancellationToken cancellationToken)
        {
            // Get the name of the type the symbol is in
            var containingOrThis = GetContainingTypeOrThis(symbol);;
            var fullName = GetFullReflectionName(containingOrThis);

            // Decompile
            document = PerformDecompilation(document, fullName, symbolCompilation, assemblyLocation);

            return Task.FromResult(document);
        }

        private static INamedTypeSymbol GetContainingTypeOrThis(Microsoft.CodeAnalysis.ISymbol symbol)
        {
            if (symbol is INamedTypeSymbol namedType)
            {
                return namedType;
            }

            return symbol.ContainingType;
        }

        private Document PerformDecompilation(Document document, string fullName, string assemblyLocation)
        {
            var _cachingDecompiler = DecompilerFactory.Get(assemblyLocation);
            var syntaxTree = _cachingDecompiler.RunToString(fullName);
            return document.WithText(SourceText.From(syntaxTree));
        }

        private Document PerformDecompilation(Document document, string fullName, Compilation compilation, string assemblyLocation)
        {
            var _cachingDecompiler = DecompilerFactory.Get(assemblyLocation);
            var syntaxTree = _cachingDecompiler.RunToString(fullName);
            return document.WithText(SourceText.From(syntaxTree));
        }

        private string GetFullReflectionName(INamedTypeSymbol containingType)
        {
            var stack = new Stack<string>();
            stack.Push(containingType.MetadataName);
            var ns = containingType.ContainingNamespace;
            do
            {
                stack.Push(ns.Name);
                ns = ns.ContainingNamespace;
            }
            while (ns != null && !ns.IsGlobalNamespace);

            return string.Join(".", stack);
        }
    }
