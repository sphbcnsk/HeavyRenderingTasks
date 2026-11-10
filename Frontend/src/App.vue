<script setup>
    import { ref, onMounted, onUnmounted } from 'vue';
    import api, { cancelTask } from './services/api';

    const isAuthenticated = ref(!!localStorage.getItem('token'));
    const isLoginMode = ref(true);
    const authForm = ref({ username: '', password: '' });
    const authError = ref('');

    const figureType = ref('Epicycloid');
    const canvasWidth = ref(1920);
    const canvasHeight = ref(1080);
    const iterations = ref(100000);
    const message = ref('');

    const tasks = ref([]);
    const adminTasks = ref([]);

    const userRole = ref('User');
    const isAdminMode = ref(false);
    let pollingInterval = null;

    const extractRoleFromToken = (token) => {
        try {
            const payload = JSON.parse(atob(token.split('.')[1]));
            return payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || payload.role || 'User';
        } catch (e) {
            return 'User';
        }
    };

    const submitAuth = async () => {
        authError.value = '';
        try {
            const endpoint = isLoginMode.value ? '/Auth/login' : '/Auth/register';
            const response = await api.post(endpoint, authForm.value);

            if (isLoginMode.value) {
                localStorage.setItem('token', response.data.token);
                isAuthenticated.value = true;
                startApp();
            } else {
                alert('Реєстрація успішна! Тепер увійдіть.');
                isLoginMode.value = true;
            }
            authForm.value.password = '';
        } catch (error) {
            authError.value = error.response?.data || 'Помилка авторизації';
        }
    };

    const logout = () => {
        localStorage.removeItem('token');
        isAuthenticated.value = false;
        isAdminMode.value = false;
        userRole.value = 'User';
        tasks.value = [];
        adminTasks.value = [];
        message.value = '';
        stopPolling();
    };

    const fetchTasks = async () => {
        if (!isAuthenticated.value) return;
        try {
            const response = await api.get('/RenderTasks');
            tasks.value = response.data.map(t => ({
                id: t.id || t.Id,
                figureType: t.figureType || t.FigureType,
                canvasWidth: t.canvasWidth || t.CanvasWidth,
                canvasHeight: t.canvasHeight || t.CanvasHeight,
                iterations: t.iterations || t.Iterations,
                status: t.status || t.Status,
                progress: t.progress || t.Progress || 0,
                resultImageUrl: t.resultImageUrl || t.ResultImageUrl
            }));
        } catch (error) {
            if (error.response?.status === 401) logout();
        }
    };

    const fetchAdminTasks = async () => {
        if (userRole.value !== 'Admin') return;
        try {
            const response = await api.get('/RenderTasks/admin/all');
            adminTasks.value = response.data;
        } catch (error) {
            console.error("Помилка завантаження адмінки:", error);
        }
    };

    const startApp = () => {
        const token = localStorage.getItem('token');
        if (token) {
            userRole.value = extractRoleFromToken(token);
        }

        fetchTasks();
        if (userRole.value === 'Admin') {
            fetchAdminTasks();
        }

        if (!pollingInterval) {
            pollingInterval = setInterval(() => {
                if (!isAdminMode.value) {
                    fetchTasks();
                } else {
                    fetchAdminTasks();
                }
            }, 1000);
        }
    };

    const stopPolling = () => {
        if (pollingInterval) {
            clearInterval(pollingInterval);
            pollingInterval = null;
        }
    };

    onMounted(() => {
        if (isAuthenticated.value) {
            startApp();
        }
    });

    onUnmounted(() => {
        stopPolling();
    });

    const submitTask = async () => {
        const maxComplexity = 10000000000000;
        const currentComplexity = canvasWidth.value * canvasHeight.value * iterations.value;

        if (currentComplexity > maxComplexity) {
            message.value = `Помилка: Перевищено ліміт обчислень! Дозволено ${maxComplexity}.`;
            return;
        }

        try {
            message.value = 'Відправка...';
            await api.post('/RenderTasks', {
                figureType: figureType.value,
                canvasWidth: canvasWidth.value,
                canvasHeight: canvasHeight.value,
                iterations: iterations.value
            });

            message.value = 'Успіх! Завдання успішно додано в чергу.';
            await fetchTasks();
        } catch (error) {
            message.value = `Помилка: ${error.response?.data?.message || error.message}`;
        }
    };

    const handleCancelTask = async (id) => {
        if (!confirm('Ви впевнені, що хочете зупинити виконання цього завдання?')) return;
        try {
            await cancelTask(id);
            await fetchTasks();
        } catch (error) {
            alert(error.response?.data?.message || "Не вдалося скасувати завдання.");
        }
    };

    const deleteTask = async (id) => {
        if (!confirm('Ви впевнені, що хочете видалити це завдання з історії?')) return;
        try {
            await api.delete(`/RenderTasks/${id}`);
            await fetchTasks();
        } catch (error) {
            alert("Не вдалося видалити завдання.");
        }
    };
</script>

<template>
    <div class="container">
        <div v-if="!isAuthenticated" class="auth-box">
            <h1>{{ isLoginMode ? 'Вхід в систему' : 'Реєстрація' }}</h1>
            <form @submit.prevent="submitAuth" class="render-form">
                <div class="form-group">
                    <label>Логін:</label>
                    <input type="text" v-model="authForm.username" required />
                </div>
                <div class="form-group">
                    <label>Пароль:</label>
                    <input type="password" v-model="authForm.password" required />
                </div>
                <button type="submit">{{ isLoginMode ? 'Увійти' : 'Зареєструватися' }}</button>
            </form>

            <div v-if="authError" class="status-message error-msg">
                {{ authError }}
            </div>

            <button @click="isLoginMode = !isLoginMode" class="link-btn">
                {{ isLoginMode ? 'Немає акаунту? Створити новий' : 'Вже є акаунт? Увійти' }}
            </button>
        </div>

        <div v-else>
            <div class="header">
                <h1>Рендерер параметричних кривих</h1>
                <button @click="logout" class="logout-btn">Вийти</button>
            </div>

            <div v-if="userRole === 'Admin'" class="admin-controls" style="margin-bottom: 20px; display: flex; gap: 10px; justify-content: center;">
                <button @click="isAdminMode = false" :style="{ backgroundColor: !isAdminMode ? '#1a5e55' : '#54bfb0' }">Мій кабінет</button>
                <button @click="isAdminMode = true" :style="{ backgroundColor: isAdminMode ? '#1a5e55' : '#54bfb0' }">Панель адміністратора</button>
            </div>

            <div v-if="isAdminMode" class="tasks-list">
                <h2>Глобальний моніторинг серверів</h2>
                <table>
                    <thead>
                        <tr>
                            <th>Сервер</th>
                            <th>Користувач</th>
                            <th>Фігура</th>
                            <th>Ітерації</th>
                            <th>Статус</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr v-for="task in adminTasks" :key="task.id">
                            <td><strong style="color: #61344B;">{{ task.serverId }}</strong></td>
                            <td>{{ task.username }}</td>
                            <td>{{ task.figureType }}</td>
                            <td>{{ task.iterations }}</td>
                            <td>
                                <strong>{{ task.status }}</strong>
                                <div v-if="task.status !== 'Виконано' && task.status !== 'Помилка' && task.status !== 'Скасовано'" style="font-size: 13px; color: #54bfb0; margin-top: 4px;">
                                    ({{ task.progress || 0 }}%)
                                </div>
                            </td>
                        </tr>
                    </tbody>
                </table>
            </div>

            <div v-else>
                <form @submit.prevent="submitTask" class="render-form">
                    <div class="form-group">
                        <label>Тип фігури:</label>
                        <select v-model="figureType">
                            <option value="Epicycloid">Епіциклоїда</option>
                            <option value="Lissajous">Фігура Ліссажу</option>
                            <option value="Rose">Полярна троянда</option>
                        </select>
                    </div>

                    <div class="form-group">
                        <label>Ширина полотна (px):</label>
                        <input type="number" v-model="canvasWidth" min="100" />
                    </div>

                    <div class="form-group">
                        <label>Висота полотна (px):</label>
                        <input type="number" v-model="canvasHeight" min="100" />
                    </div>

                    <div class="form-group">
                        <label>Кількість ітерацій:</label>
                        <input type="number" v-model="iterations" min="100" />
                    </div>

                    <button type="submit">Створити задачу</button>
                </form>

                <div v-if="message" class="status-message">
                    {{ message }}
                </div>

                <div class="tasks-list" v-if="tasks.length > 0">
                    <h2>Ваші завдання</h2>
                    <table>
                        <thead>
                            <tr>
                                <th>Фігура</th>
                                <th>Розмір (px)</th>
                                <th>Ітерації</th>
                                <th>Статус</th>
                                <th>Результат</th>
                                <th>Дії</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr v-for="task in tasks" :key="task.id">
                                <td>{{ task.figureType }}</td>
                                <td>{{ task.canvasWidth }} x {{ task.canvasHeight }}</td>
                                <td>{{ task.iterations }}</td>
                                <td>
                                    <strong>{{ task.status }}</strong>
                                    <div v-if="task.status !== 'Виконано' && task.status !== 'Помилка' && task.status !== 'Скасовано'" style="font-size: 13px; color: #54bfb0; margin-top: 4px;">
                                        ({{ task.progress || 0 }}%)
                                    </div>
                                </td>
                                <td>
                                    <img v-if="task.resultImageUrl" :src="`https://localhost:5005${task.resultImageUrl}`" alt="Крива" height="60" style="border-radius: 4px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); cursor: pointer;" title="Відкрити зображення" onclick="window.open(this.src, '_blank')" />
                                    <span v-else>—</span>
                                </td>
                                <td>
                                    <button v-if="task.status === 'В черзі' || task.status === 'Початок малювання...' || task.status.includes('Малювання')"
                                            @click="handleCancelTask(task.id)"
                                            class="cancel-btn">
                                        Скасувати
                                    </button>
                                    <button v-else
                                            @click="deleteTask(task.id)"
                                            class="delete-btn">
                                        Видалити
                                    </button>
                                </td>
                            </tr>
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    </div>
</template>

<style scoped>
    .container {
        max-width: 700px;
        margin: 40px auto;
        padding: 20px;
        font-family: 'Montserrat', sans-serif;
    }

    .header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 24px;
    }

    h1 {
        color: #61344B;
        text-align: center;
        margin: 0;
    }

    .auth-box h1 {
        margin-bottom: 24px;
    }

    .render-form {
        display: flex;
        flex-direction: column;
        gap: 16px;
        background: #ffffff;
        padding: 24px;
        border-radius: 12px;
        box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
    }

    .form-group {
        display: flex;
        flex-direction: column;
    }

    label {
        font-weight: 600;
        margin-bottom: 8px;
        color: #1a5e55;
    }

    input, select {
        padding: 10px;
        border: 1px solid #ccc;
        border-radius: 8px;
        font-family: inherit;
        font-size: 14px;
    }

    button {
        padding: 12px;
        background-color: #54bfb0;
        color: white;
        border: none;
        border-radius: 8px;
        cursor: pointer;
        font-weight: 700;
        font-size: 16px;
        margin-top: 10px;
        transition: background-color 0.2s;
    }

        button:hover {
            background-color: #1a5e55;
        }

    .logout-btn {
        background-color: #e74c3c;
        padding: 8px 16px;
        font-size: 14px;
        margin-top: 0;
    }

        .logout-btn:hover {
            background-color: #c0392b;
        }

    .link-btn {
        background: none;
        color: #1a5e55;
        text-decoration: underline;
        width: 100%;
        margin-top: 16px;
        box-shadow: none;
    }

        .link-btn:hover {
            background: none;
            color: #0b5c53;
        }

    .status-message {
        margin-top: 24px;
        padding: 16px;
        background-color: #EFD6AC;
        color: #61344B;
        border-radius: 8px;
        font-weight: 600;
        text-align: center;
    }

    .error-msg {
        background-color: #ffeaea;
        color: #d93025;
    }

    .tasks-list {
        margin-top: 30px;
        background: white;
        padding: 20px;
        border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.05);
    }

        .tasks-list h2 {
            color: #4a3b4e;
            margin-bottom: 15px;
            font-size: 1.5rem;
        }

    table {
        width: 100%;
        border-collapse: collapse;
        text-align: left;
    }

    th, td {
        padding: 12px;
        border-bottom: 1px solid #eee;
    }

    th {
        background-color: #f8f9fa;
        color: #0b5c53;
        font-weight: bold;
    }

    tr:hover {
        background-color: #fcfcfc;
    }

    .cancel-btn {
        background-color: #f39c12;
        padding: 6px 12px;
        font-size: 12px;
        margin-top: 0;
        color: white;
        border: none;
        border-radius: 4px;
        cursor: pointer;
    }

        .cancel-btn:hover {
            background-color: #d68910;
        }

    .delete-btn {
        background-color: #e74c3c;
        padding: 6px 12px;
        font-size: 12px;
        margin-top: 0;
        color: white;
        border: none;
        border-radius: 4px;
        cursor: pointer;
    }

        .delete-btn:hover {
            background-color: #c0392b;
        }
</style>