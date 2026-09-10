window.timeZoneHelper = {
    getUserTimeZone: function () {
        try {
            return Intl.DateTimeFormat().resolvedOptions().timeZone || "";
        } catch (e) {
            return "";
        }
    },
    getUserLocale: function () {
        try {
            return navigator.language || navigator.userLanguage || "";
        } catch (e) {
            return "";
        }
    }
};
