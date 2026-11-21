using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using DiskManager.Models;

namespace DiskManager.ViewModels;

public sealed class SelectionSummary : IDisposable
{
    private readonly ObservableCollection<FileItem> _items;
    private readonly HashSet<FileItem> _tracked = new();

    public SelectionSummary(ObservableCollection<FileItem> items)
    {
        _items = items;
        _items.CollectionChanged += OnCollectionChanged;

        foreach (var item in _items)
        {
            Track(item);
        }
    }

    public event EventHandler? Changed;

    public int TotalCount => _items.Count;
    public long TotalSize => _items.Sum(item => item.Size);
    public int SelectedCount => _items.Count(item => item.IsSelected);
    public long SelectedSize => _items.Where(item => item.IsSelected).Sum(item => item.Size);

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var item in _tracked.ToList())
            {
                Untrack(item);
            }

            foreach (var item in _items)
            {
                Track(item);
            }
        }
        else
        {
            if (e.OldItems != null)
            {
                foreach (FileItem item in e.OldItems)
                {
                    Untrack(item);
                }
            }

            if (e.NewItems != null)
            {
                foreach (FileItem item in e.NewItems)
                {
                    Track(item);
                }
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Track(FileItem item)
    {
        if (_tracked.Add(item))
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }
    }

    private void Untrack(FileItem item)
    {
        if (_tracked.Remove(item))
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FileItem.IsSelected) or nameof(FileItem.Size))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        _items.CollectionChanged -= OnCollectionChanged;
        foreach (var item in _tracked.ToList())
        {
            Untrack(item);
        }

        _tracked.Clear();
    }
}