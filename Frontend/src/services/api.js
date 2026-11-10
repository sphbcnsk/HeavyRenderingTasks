import axios from 'axios';

const api = axios.create({
    baseURL: 'https://localhost:5005/api',
    headers: {
        'Content-Type': 'application/json'
    }
});

api.interceptors.request.use(config => {
    const token = localStorage.getItem('token');
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
});

export const cancelTask = (id) => api.post(`/RenderTasks/${id}/cancel`);

export default api;