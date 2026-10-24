using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicFlow.Ois
{
    public static class DomainRules
    {
        public static T Get<T>(List<T> values, string id)
            where T : IOisEntity =>
            values.SingleOrDefault(x => x.Id == id)
            ?? throw new BusinessException("not_found", "记录不存在", BusinessErrorKind.NotFound);

        public static T Current<T>(List<T> values, string id, int version)
            where T : IOisEntity
        {
            var value = Get(values, id);
            Need(value.Version == version, "version_conflict", "记录已更新，请刷新后重新操作");
            return value;
        }

        public static DateTime Time(DateTimeOffset? value) =>
            value?.UtcDateTime ?? throw new BusinessException("invalid_time", "请选择起止时间", BusinessErrorKind.InvalidInput);

        public static string Text(string? value)
        {
            Need(
                !string.IsNullOrWhiteSpace(value) && value.Length <= 4000,
                "invalid_text",
                "请填写1至4000字符的内容或原因", BusinessErrorKind.InvalidInput);
            return value!.Trim();
        }

        public static void Need(bool condition, string code, string message, BusinessErrorKind kind = BusinessErrorKind.Conflict)
        {
            if (!condition)
                throw new BusinessException(code, message, kind);
        }
    }
}
