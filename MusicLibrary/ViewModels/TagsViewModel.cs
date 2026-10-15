using System.Collections.ObjectModel;
using System.Windows.Input;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicLibrary.Commands;

namespace MusicLibrary.ViewModels;

/// <summary>
/// ViewModel окна управления тегами. Создаёт/переименовывает/удаляет теги через
/// ITagRepository. Каждый CRUD-вызов синхронно перечитывает список — данных мало,
/// async и change-tracking тут оверинжиниринг.
/// </summary>
public sealed class TagsViewModel : ViewModelBase
{
    /// <summary>Палитра предустановленных цветов для быстрого выбора. Кастомный hex — позже.</summary>
    public IReadOnlyList<string> PresetColors { get; } = new[]
    {
        "#D4A574", // фирменный золотой
        "#7BB374", // зелёный
        "#74A9D4", // голубой
        "#C77DFF", // фиолетовый
        "#FF6B6B", // красный
        "#FFD166", // жёлтый
        "#9C9C9C"  // серый
    };

    private readonly ITagRepository _repository;
    private string _newTagName = string.Empty;
    private string? _selectedColor;
    private Tag? _selectedTag;
    private string? _renameInput;
    private string? _errorMessage;

    public TagsViewModel(ITagRepository repository)
    {
        _repository = repository;
        Tags = new ObservableCollection<Tag>(_repository.GetAll());
        _selectedColor = PresetColors[0];

        CreateTagCommand = new RelayCommand(_ => ExecuteStorage(CreateTag), _ => !string.IsNullOrWhiteSpace(NewTagName));
        DeleteTagCommand = new RelayCommand(_ => ExecuteStorage(DeleteTag), _ => SelectedTag is not null);
        RenameTagCommand = new RelayCommand(_ => ExecuteStorage(RenameTag),
            _ => SelectedTag is not null && !string.IsNullOrWhiteSpace(RenameInput));
        SetColorCommand = new RelayCommand(c => ExecuteStorage(() => SetColor(c as string)), _ => SelectedTag is not null);
        // Палитра в блоке «Новый тег» просто меняет SelectedColor без обращения к репозиторию.
        PickNewColorCommand = new RelayCommand(c => SelectedColor = c as string);
    }

    public ObservableCollection<Tag> Tags { get; }

    public string NewTagName
    {
        get => _newTagName;
        set
        {
            if (SetProperty(ref _newTagName, value ?? string.Empty))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string? SelectedColor
    {
        get => _selectedColor;
        set => SetProperty(ref _selectedColor, value);
    }

    public Tag? SelectedTag
    {
        get => _selectedTag;
        set
        {
            if (SetProperty(ref _selectedTag, value))
            {
                RenameInput = value?.Name;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string? RenameInput
    {
        get => _renameInput;
        set
        {
            if (SetProperty(ref _renameInput, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ICommand CreateTagCommand { get; }
    public ICommand DeleteTagCommand { get; }
    public ICommand RenameTagCommand { get; }
    public ICommand SetColorCommand { get; }
    public ICommand PickNewColorCommand { get; }

    private void ExecuteStorage(Action action)
    {
        try { action(); }
        catch (Exception ex) when (Services.StorageErrors.IsExpected(ex))
        {
            System.Diagnostics.Trace.TraceError("Tag operation: {0}", ex);
            ErrorMessage = $"Не удалось сохранить теги: {ex.Message}";
        }
    }

    private void CreateTag()
    {
        try
        {
            var saved = _repository.Add(new Tag { Name = NewTagName.Trim(), Color = SelectedColor });
            Tags.Add(saved);
            ReorderTags();
            NewTagName = string.Empty;
            ErrorMessage = null;
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void DeleteTag()
    {
        if (SelectedTag is null) return;
        _repository.Remove(SelectedTag.Id);
        Tags.Remove(SelectedTag);
        SelectedTag = null;
        ErrorMessage = null;
    }

    private void RenameTag()
    {
        if (SelectedTag is null || string.IsNullOrWhiteSpace(RenameInput)) return;
        try
        {
            var updated = new Tag { Id = SelectedTag.Id, Name = RenameInput.Trim(), Color = SelectedTag.Color };
            _repository.Update(updated);
            int idx = Tags.IndexOf(SelectedTag);
            if (idx >= 0)
            {
                Tags[idx] = updated;
                SelectedTag = updated;
            }
            ReorderTags();
            ErrorMessage = null;
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void SetColor(string? color)
    {
        if (SelectedTag is null) return;
        var updated = new Tag { Id = SelectedTag.Id, Name = SelectedTag.Name, Color = color };
        _repository.Update(updated);
        int idx = Tags.IndexOf(SelectedTag);
        if (idx >= 0)
        {
            Tags[idx] = updated;
            SelectedTag = updated;
        }
    }

    private void ReorderTags()
    {
        // Простой in-place sort по имени; коллекция маленькая, накладные расходы нулевые.
        var sorted = Tags.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            int currentIndex = Tags.IndexOf(sorted[i]);
            if (currentIndex != i)
            {
                Tags.Move(currentIndex, i);
            }
        }
    }
}
