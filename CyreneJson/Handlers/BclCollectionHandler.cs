using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CyreneJson.Attributes;

namespace CyreneJson.Handlers;

[CyreneHandler(typeof(List<>))]
[CyreneHandler(typeof(Collection<>))]
[CyreneHandler(typeof(ReadOnlyCollection<>))]
[CyreneHandler(typeof(ObservableCollection<>))]
[CyreneHandler(typeof(ReadOnlyObservableCollection<>))]
[CyreneHandler(typeof(IList<>))]
[CyreneHandler(typeof(IReadOnlyList<>))]
[CyreneHandler(typeof(ICollection<>))]
[CyreneHandler(typeof(IEnumerable<>))]
[CyreneHandler(typeof(IReadOnlyCollection<>))]
[CyreneHandler(typeof(HashSet<>))]
[CyreneHandler(typeof(SortedSet<>))]
[CyreneHandler(typeof(ISet<>))]
[CyreneHandler(typeof(IReadOnlySet<>))]
[CyreneHandler(typeof(Queue<>))]
[CyreneHandler(typeof(Stack<>))]
[CyreneHandler(typeof(LinkedList<>))]
[CyreneHandler(typeof(ConcurrentQueue<>))]
[CyreneHandler(typeof(ConcurrentStack<>))]
[CyreneHandler(typeof(ConcurrentBag<>))]
[CyreneHandler(typeof(BlockingCollection<>))]
[CyreneHandler(typeof(Dictionary<,>))]
[CyreneHandler(typeof(IDictionary<,>))]
[CyreneHandler(typeof(IReadOnlyDictionary<,>))]
[CyreneHandler(typeof(SortedDictionary<,>))]
[CyreneHandler(typeof(SortedList<,>))]
[CyreneHandler(typeof(ConcurrentDictionary<,>))]
public class BclCollectionHandler;
