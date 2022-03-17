using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
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

            var otherPath = symbol.GetFilePathForExternalSymbol(document.Project);

            if (null == null)
            {
                //var metadataLocation = externalSourceService.GetExternalSymbolLocation(symbol, metadataDocument, CancellationToken.None).GetAwaiter().GetResult();
                //var mappedSpan = metadataLocation.GetMappedLineSpan();

                //SourceText sourceText = null;
                try
                {
                    //sourceText = metadataDocument.GetTextAsync().Result;
                }
                catch (Exception)
                {
                }


                //string text = null;

                //if (sourceText!= null)
                //{
                    //text = GetLineText(location, sourceText, mappedSpan.StartLinePosition.Line);
                //}

                var metadatSource = new MetadataSource()
                {
                    AssemblyName = symbol.ContainingAssembly.Name,
                    ProjectName = document.Project.Name,
                    TypeName = symbol.GetSymbolName(),
                    Line = 1,
                    Column = 1,
                    //Line = mappedSpan.StartLinePosition.Line + 1,
                    //Column = mappedSpan.StartLinePosition.Character + 1,
                    FileName = otherPath,
                    SourceText = "sourceText?.ToString()",
                    SourceLine = symbol.GetMetadataName(),
                    MemberName = symbol.MetadataName
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

        static SourceText GetSourceText(Location location, IEnumerable<Document> documents, bool hasMappedPath)
        {
            // if we have a mapped linespan and we found a corresponding document, pick that one
            // otherwise use the SourceText of current location
            if (hasMappedPath)
            {
                SourceText source = null;
                if (documents != null && documents.FirstOrDefault(d => d != null && d.TryGetText(out source)) != null)
                {
                    // we have a mapped document that exists in workspace
                    return source;
                }

                // we have a mapped document that doesn't exist in workspace
                // in that case we have to always fall back to original linespan
                return null;
            }

            // unmapped document so just continue with current SourceText
            return location.SourceTree.GetText();
        }

        static string GetLineText(Location location, SourceText sourceText, int startLine)
        {
            // bounds check in case the mapping is incorrect, since user can put whatever line they want
            if (sourceText != null && sourceText.Lines.Count > startLine)
            {
                return sourceText.Lines[startLine].ToString();
            }

            // in case we fall out of bounds, we shouldn't crash, fallback to text from the unmapped span and the current file
            var fallBackLineSpan = location.GetLineSpan();
            return location.SourceTree.GetText().Lines[fallBackLineSpan.StartLinePosition.Line].ToString();
        }
    }
}
