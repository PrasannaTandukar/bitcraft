import { createSlice } from "@reduxjs/toolkit";

const THEME_MODE_KEY = "themeMode";
const TABLE_DENSITY_KEY = "tableDensity";

const readThemeMode = () => {
    const stored = localStorage.getItem(THEME_MODE_KEY);
    return stored === "dark" ? "dark" : "light";
};

const readTableDensity = () => {
    const stored = localStorage.getItem(TABLE_DENSITY_KEY);
    return ["small", "middle", "large"].includes(stored) ? stored : "middle";
};

const preferencesSlice = createSlice({
    name: "preferences",
    initialState: {
        themeMode: readThemeMode(),
        tableDensity: readTableDensity(),
    },
    reducers: {
        setThemeMode(state, action) {
            state.themeMode = action.payload === "dark" ? "dark" : "light";
            localStorage.setItem(THEME_MODE_KEY, state.themeMode);
        },
        setTableDensity(state, action) {
            state.tableDensity = ["small", "middle", "large"].includes(action.payload) ? action.payload : "middle";
            localStorage.setItem(TABLE_DENSITY_KEY, state.tableDensity);
        },
    },
});

export const { setThemeMode, setTableDensity } = preferencesSlice.actions;
export default preferencesSlice.reducer;
