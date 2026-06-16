using System.Collections.Concurrent;
using CyreneJson.Attributes;

namespace CyreneJson.Handlers;

[CyreneHandler(typeof(List<>), CollectionKind.List)]
[CyreneHandler(typeof(IList<>), CollectionKind.List)]
[CyreneHandler(typeof(IReadOnlyList<>), CollectionKind.List)]
[CyreneHandler(typeof(ICollection<>), CollectionKind.List)]
[CyreneHandler(typeof(IEnumerable<>), CollectionKind.List)]
[CyreneHandler(typeof(IReadOnlyCollection<>), CollectionKind.List)]
[CyreneHandler(typeof(HashSet<>), CollectionKind.List)]
[CyreneHandler(typeof(ISet<>), CollectionKind.List)]
[CyreneHandler(typeof(IReadOnlySet<>), CollectionKind.List)]
[CyreneHandler(typeof(Dictionary<,>), CollectionKind.Dictionary)]
[CyreneHandler(typeof(IDictionary<,>), CollectionKind.Dictionary)]
[CyreneHandler(typeof(IReadOnlyDictionary<,>), CollectionKind.Dictionary)]
[CyreneHandler(typeof(SortedDictionary<,>), CollectionKind.Dictionary)]
[CyreneHandler(typeof(SortedList<,>), CollectionKind.Dictionary)]
[CyreneHandler(typeof(ConcurrentDictionary<,>), CollectionKind.Dictionary)]
public class BclCollectionHandler;
