using System;

namespace UnturnedImages.Module.Images
{
    internal static class ExportProgressTracker
    {
        private static int _queued;
        private static int _completed;
        private static int _failed;
        private static string _label = "Ожидание";
        private static string _lastMessage = "Готово";

        public static int Queued => _queued;

        public static int Completed => _completed;

        public static int Failed => _failed;

        public static bool IsActive => _queued > 0 && _completed < _queued;

        public static float Progress => _queued <= 0 ? 0f : Math.Min(1f, _completed / (float)_queued);

        public static string StatusText =>
            _queued <= 0
                ? _lastMessage
                : $"{_label}: {_completed}/{_queued} ({Progress:P0}), ошибок: {_failed}. {_lastMessage}";

        public static void Reset(string label = "Ожидание")
        {
            _queued = 0;
            _completed = 0;
            _failed = 0;
            _label = label;
            _lastMessage = "Готово";
        }

        public static void AddQueued(string label, int count)
        {
            if (count <= 0)
            {
                _lastMessage = "Нечего экспортировать по текущим фильтрам";
                return;
            }

            if (!IsActive)
            {
                _queued = 0;
                _completed = 0;
                _failed = 0;
            }

            _queued += count;
            _label = label;
            _lastMessage = "Добавлено в очередь";
        }

        public static void CompleteOne(string message, bool success)
        {
            if (_queued <= 0)
            {
                return;
            }

            _completed++;
            if (!success)
            {
                _failed++;
            }

            _lastMessage = message;

            if (_completed >= _queued)
            {
                _lastMessage = _failed == 0 ? "Экспорт завершен" : "Экспорт завершен с ошибками";
            }
        }
    }
}
