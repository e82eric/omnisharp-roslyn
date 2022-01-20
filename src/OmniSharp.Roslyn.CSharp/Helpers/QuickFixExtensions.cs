using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using OmniSharp.Extensions;
using OmniSharp.Models;
using OmniSharp.Models.Metadata;
using OmniSharp.Models.V2;
using OmniSharp.Roslyn;
using Location = Microsoft.CodeAnalysis.Location;

namespace OmniSharp.Helpers
{
    internal static class QuickFixExtensions
    {
        internal static void Add(this ICollection<QuickFix> quickFixes, ISymbol symbol, OmniSharpWorkspace workspace)
        {
            foreach (var location in symbol.Locations)
            {
                quickFixes.Add(location, workspace);
            }
        }

        internal static void Add(this ICollection<QuickFix> quickFixes, Location location, OmniSharpWorkspace workspace)
        {
            if (location.IsInSource)
            {
                var quickFix = location.GetQuickFix(workspace);
                quickFixes.Add(quickFix);
            }
        }

        internal static void Add(this ICollection<QuickFix> quickFixes, ICollection<MetadataSource> metadataSources, ISymbol symbol, Location location, OmniSharpWorkspace workspace, Document document, IExternalSourceService externalSourceService)
        {
            var (metadataDocument, documentPath) = externalSourceService.GetAndAddExternalSymbolDocument(document.Project, symbol, CancellationToken.None).GetAwaiter().GetResult();

            if (metadataDocument != null)
            {
                var metadataLocation = externalSourceService.GetExternalSymbolLocation(symbol, metadataDocument, CancellationToken.None).GetAwaiter().GetResult();
                var mappedSpan = metadataLocation.GetMappedLineSpan();

                var metadatSource = new MetadataSource()
                {
                    AssemblyName = symbol.ContainingAssembly.Name,
                    ProjectName = document.Project.Name,
                    TypeName = symbol.GetSymbolName(),
                    Line = mappedSpan.StartLinePosition.Line + 1,
                    Column = mappedSpan.StartLinePosition.Character + 1,
                    FileName = documentPath
                };

                metadataSources.Add(metadatSource);
            }
        }

        internal static void AddRange(this ICollection<QuickFix> quickFixes, IEnumerable<ISymbol> symbols, OmniSharpWorkspace workspace)
        {
            foreach (var symbol in symbols)
            {
                foreach (var location in symbol.Locations)
                {
                    quickFixes.Add(location, workspace);
                }
            }
        }
    }
}
