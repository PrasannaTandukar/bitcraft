import { createSlice, createAsyncThunk } from "@reduxjs/toolkit";

const AUTH_API_URL = "http://localhost:8000/api/auth";

const ROLE_CLAIM =
    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

const decodeRoles = (token) => {
  try {
    const payload = JSON.parse(atob(token.split(".")[1]));
    const roles = payload[ROLE_CLAIM];

    if (!roles) return [];

    return Array.isArray(roles) ? roles : [roles];
  } catch {
    return [];
  }
};

const getBackendErrorMessage = async (response) => {
  let data = null;

  try {
    data = await response.json();
  } catch {
    return "Something went wrong. Please try again.";
  }

  console.log("Backend error response:", data);

  // Case 1: Backend returns plain array
  // Example: ["Email already exists"]
  if (Array.isArray(data)) {
    return data.join(" ");
  }

  // Case 2: ASP.NET validation error format
  // Example:
  // {
  //   errors: {
  //     Email: ["Email already exists"],
  //     Password: ["Password is too short"]
  //   }
  // }
  if (data?.errors && typeof data.errors === "object") {
    const errors = Object.values(data.errors)
        .flat()
        .filter(Boolean);

    if (errors.length > 0) {
      return errors.join(" ");
    }
  }

  // Case 3: Common backend error fields
  // Example:
  // { message: "Email already exists" }
  // { error: "Invalid credentials" }
  // { title: "One or more validation errors occurred." }
  // { detail: "User already exists." }
  if (data?.message) return data.message;
  if (data?.error) return data.error;
  if (data?.detail) return data.detail;
  if (data?.title) return data.title;

  // Case 4: Backend returns string
  if (typeof data === "string") {
    return data;
  }

  if (response.status === 400) {
    return "Please check your details and try again.";
  }

  if (response.status === 401) {
    return "Invalid email or password.";
  }

  if (response.status === 409) {
    return "An account with this email already exists.";
  }

  return "Something went wrong. Please try again.";
};

const persistAuthSession = (data) => {
  const token = data.token || data.accessToken;

  if (!token) {
    throw new Error("Authentication succeeded, but no token was returned.");
  }

  const email = data.email || data.user?.email || null;
  const roles = data.roles?.length ? data.roles : decodeRoles(token);

  localStorage.setItem("token", token);
  localStorage.setItem("user", JSON.stringify(email));
  localStorage.setItem("roles", JSON.stringify(roles));

  return {
    token,
    user: email,
    roles,
  };
};

export const loginUser = createAsyncThunk(
    "auth/loginUser",
    async ({ email, password }, { rejectWithValue }) => {
      try {
        const response = await fetch(`${AUTH_API_URL}/login`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            email,
            password,
          }),
        });

        if (!response.ok) {
          const errorMessage = await getBackendErrorMessage(response);
          return rejectWithValue(errorMessage);
        }

        const data = await response.json();
        return persistAuthSession(data);
      } catch (err) {
        return rejectWithValue(
            err.message || "Could not connect to the server. Please try again.",
        );
      }
    },
);

export const registerUser = createAsyncThunk(
    "auth/registerUser",
    async ({ fullName, email, password }, { rejectWithValue }) => {
      try {
        const response = await fetch(`${AUTH_API_URL}/register`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            fullName,
            email,
            password,
          }),
        });

        if (!response.ok) {
          const errorMessage = await getBackendErrorMessage(response);
          return rejectWithValue(errorMessage);
        }

        const data = await response.json();
        return persistAuthSession(data);
      } catch (err) {
        return rejectWithValue(
            err.message || "Could not connect to the server. Please try again.",
        );
      }
    },
);

const getStoredUser = () => {
  try {
    return JSON.parse(localStorage.getItem("user")) || null;
  } catch {
    localStorage.removeItem("user");
    localStorage.removeItem("token");
    localStorage.removeItem("roles");
    return null;
  }
};

const getStoredRoles = () => {
  try {
    return JSON.parse(localStorage.getItem("roles")) || [];
  } catch {
    localStorage.removeItem("roles");
    return [];
  }
};

const authSlice = createSlice({
  name: "auth",
  initialState: {
    token: localStorage.getItem("token") || null,
    user: getStoredUser(),
    roles: getStoredRoles(),
    loading: false,
    error: null,
  },
  reducers: {
    logout(state) {
      state.token = null;
      state.user = null;
      state.roles = [];
      state.error = null;
      state.loading = false;

      localStorage.removeItem("token");
      localStorage.removeItem("user");
      localStorage.removeItem("roles");
    },

    clearError(state) {
      state.error = null;
    },
  },
  extraReducers: (builder) => {
    builder

        // Login
        .addCase(loginUser.pending, (state) => {
          state.loading = true;
          state.error = null;
        })
        .addCase(loginUser.fulfilled, (state, action) => {
          state.loading = false;
          state.error = null;
          state.token = action.payload.token;
          state.user = action.payload.user;
          state.roles = action.payload.roles;
        })
        .addCase(loginUser.rejected, (state, action) => {
          state.loading = false;
          state.error = action.payload || "Login failed.";
        })

        // Register
        .addCase(registerUser.pending, (state) => {
          state.loading = true;
          state.error = null;
        })
        .addCase(registerUser.fulfilled, (state, action) => {
          state.loading = false;
          state.error = null;
          state.token = action.payload.token;
          state.user = action.payload.user;
          state.roles = action.payload.roles;
        })
        .addCase(registerUser.rejected, (state, action) => {
          state.loading = false;
          state.error = action.payload || "Registration failed.";
        });
  },
});

export const { logout, clearError } = authSlice.actions;

export default authSlice.reducer;