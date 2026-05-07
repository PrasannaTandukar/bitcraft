import { createSlice, createAsyncThunk } from "@reduxjs/toolkit";

// Async thunk simulating an API login call
export const loginUser = createAsyncThunk(
  "auth/loginUser",
  async ({ email, password }, { rejectWithValue }) => {
    try {
      // Replace with your real API call
      const response = await fetch("http://localhost:8000/api/Auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });

      if (!response.ok) throw new Error("Invalid credentials");

      const data = await response.json();
      console.log(data);
      // Persist to localStorage
      // ✅ Save and return email explicitly
      localStorage.setItem("token", data.token);
      localStorage.setItem("user", JSON.stringify(data.email));

      return { token: data.token, user: data.email }; // 👈 map email to user
    } catch (err) {
      return rejectWithValue(err.message);
    }
  },
);
const getStoredUser = () => {
  try {
    return JSON.parse(localStorage.getItem("user")) || null;
  } catch {
    localStorage.removeItem("user"); // delete corrupt value
    localStorage.removeItem("token"); // clear token too for consistency
    return null;
  }
};

const authSlice = createSlice({
  name: "auth",
  initialState: {
    token: localStorage.getItem("token") || null,
    user: getStoredUser() || null,
    loading: false,
    error: null,
  },
  reducers: {
    logout(state) {
      state.token = null;
      state.user = null;
      state.error = null;
      localStorage.removeItem("token");
      localStorage.removeItem("user");
    },
    clearError(state) {
      state.error = null;
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(loginUser.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(loginUser.fulfilled, (state, action) => {
        state.loading = false;
        state.token = action.payload.token;
        state.user = action.payload.user;
      })
      .addCase(loginUser.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload;
      });
  },
});

export const { logout, clearError } = authSlice.actions;
export default authSlice.reducer;
