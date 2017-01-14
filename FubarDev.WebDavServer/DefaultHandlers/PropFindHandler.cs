﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using FubarDev.WebDavServer.FileSystem;
using FubarDev.WebDavServer.Handlers;
using FubarDev.WebDavServer.Model;
using FubarDev.WebDavServer.Properties;

using JetBrains.Annotations;

namespace FubarDev.WebDavServer.DefaultHandlers
{
    public class PropFindHandler : IPropFindHandler
    {
        private readonly IWebDavHost _host;

        public PropFindHandler(IFileSystem fileSystem, IWebDavHost host)
        {
            _host = host;
            FileSystem = fileSystem;
        }

        public IEnumerable<string> HttpMethods { get; } = new[] { "PROPFIND" };

        public IFileSystem FileSystem { get; }

        public async Task<IWebDavResult> HandleAsync(string path, Propfind request, Depth depth, CancellationToken cancellationToken)
        {
            if (depth == Depth.Infinity)
            {
                // Not supported yet
                return new WebDavResult<Error1>(WebDavStatusCodes.Forbidden, new Error1()
                {
                    ItemsElementName = new[] {ItemsChoiceType2.PropfindFiniteDepth,},
                    Items = new[] {new object(),}
                });
            }

            var selectionResult = await FileSystem.SelectAsync(path, cancellationToken).ConfigureAwait(false);
            if (selectionResult.IsMissing)
            {
                throw new WebDavException(WebDavStatusCodes.NotFound);
            }

            var entries = new List<IEntry>();
            if (selectionResult.ResultType == SelectionResultType.FoundDocument)
            {
                entries.Add(selectionResult.Document);
            }
            else
            {
                Debug.Assert(selectionResult.ResultType == SelectionResultType.FoundCollection);
                entries.Add(selectionResult.Collection);
                if (depth != Depth.Zero)
                {
                    var children = await selectionResult.Collection.GetChildrenAsync(cancellationToken).ConfigureAwait(false);

                    using (var entriesEnumerator = selectionResult.Collection.GetEntries(children, depth.OrderValue - 1).GetEnumerator())
                    {
                        while (await entriesEnumerator.MoveNext(cancellationToken).ConfigureAwait(false))
                        {
                            entries.Add(entriesEnumerator.Current);
                        }
                    }
                }
            }

            if (request == null)
                return await HandleAllPropAsync(entries, cancellationToken).ConfigureAwait(false);

            switch (request.ItemsElementName[0])
            {
                case ItemsChoiceType.Allprop:
                    return await HandleAllPropAsync(request, entries, cancellationToken).ConfigureAwait(false);
            }

            throw new WebDavException(WebDavStatusCodes.Forbidden);
        }

        private Task<IWebDavResult> HandleAllPropAsync([NotNull] Propfind request, IReadOnlyCollection<IEntry> entries, CancellationToken cancellationToken)
        {
            var include = request.ItemsElementName.Select((x, i) => Tuple.Create(x, i)).Where(x => x.Item1 == ItemsChoiceType.Include).Select(x => (Include)request.Items[x.Item2]).FirstOrDefault();
            return HandleAllPropAsync(include, entries, cancellationToken);
        }

        private Task<IWebDavResult> HandleAllPropAsync(IReadOnlyCollection<IEntry> entries, CancellationToken cancellationToken)
        {
            return HandleAllPropAsync((Include)null, entries, cancellationToken);
        }

        private async Task<IWebDavResult> HandleAllPropAsync([CanBeNull] Include include, IReadOnlyCollection<IEntry> entries, CancellationToken cancellationToken)
        {
            var responses = new List<Response>();
            foreach (var entry in entries)
            {
                var response = await CreateEntryResponseAsync(entry, 0, cancellationToken).ConfigureAwait(false);
                responses.Add(response);
            }

            var result = new Multistatus()
            {
                Response = responses.ToArray()
            };

            return new WebDavResult<Multistatus>(WebDavStatusCodes.MultiStatus, result);
        }

        private async Task<Response> CreateEntryResponseAsync(IEntry entry, int allowedExpense, CancellationToken cancellationToken)
        {
            var href = new Uri(_host.BaseUrl, entry.Path);
            var propElements = new List<XElement>();
            foreach (var property in entry.Properties.OfType<IUntypedReadableProperty>().Where(x => x.Cost <= allowedExpense))
            {
                var element = await property.GetXmlValueAsync(cancellationToken).ConfigureAwait(false);
                propElements.Add(element);
            }

            return new Response()
            {
                Href = href.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped),
                ItemsElementName = new []{ ItemsChoiceType1.Propstat, ItemsChoiceType1.Status, },
                Items = new object[]
                {
                    new Propstat()
                    {
                        Prop = new Prop()
                        {
                            Any = propElements.ToArray(),
                        },
                    }, 
                    $"{_host.RequestProtocol} 200 OK"
                }
            };
        }
    }
}
