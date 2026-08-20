import { configureStore } from "@reduxjs/toolkit";
import authReducer from "./authSlice";
import preferencesReducer from "./preferencesSlice";

const store = configureStore({
  reducer: {
    auth: authReducer,
    preferences: preferencesReducer,
  },
});

export default store;
