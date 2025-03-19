using System.Collections.Generic;

public class SimplePriorityQueue<T>
{
    private List<(T item, float priority)> elements = new List<(T, float)>();
    
    public int Count => elements.Count;
    
    public void Enqueue(T item, float priority)
    {
        elements.Add((item, priority));
    }
    
    public T Dequeue()
    {
        int bestIndex = 0;
        
        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i].priority < elements[bestIndex].priority)
            {
                bestIndex = i;
            }
        }
        
        T bestItem = elements[bestIndex].item;
        elements.RemoveAt(bestIndex);
        return bestItem;
    }
} 