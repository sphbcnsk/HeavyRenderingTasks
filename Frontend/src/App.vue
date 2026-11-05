<script setup>
    import { ref, onMounted, onUnmounted } from 'vue';
    import api from './services/api';

    const figureType = ref('Epicycloid');
    const canvasWidth = ref(1920);
    const canvasHeight = ref(1080);
    const iterations = ref(100000);
    const message = ref('');

    const tasks = ref([]);
    let pollingInterval = null;

    const fetchTasks = async () => {
        try {
            const response = await api.get('/RenderTasks');
            tasks.value = response.data;
        } catch (error) {
            console.error("Помилка завантаження списку:", error);
        }
    };

    onMounted(() => {
        fetchTasks();
        pollingInterval = setInterval(fetchTasks, 500);
    });

    onUnmounted(() => {
        if (pollingInterval) clearInterval(pollingInterval);
    });

    const submitTask = async () => {
        const maxComplexity = 10000000000000; 
        const currentComplexity = canvasWidth.value * canvasHeight.value * iterations.value;

        if (currentComplexity > maxComplexity) {
            message.value = `Помилка клієнта: Перевищено ліміт обчислень! Дозволено ${maxComplexity}, у вас ${currentComplexity}. Зменште розмір або кількість ітерацій.`;
            return;
        }

        try {
            message.value = 'Відправка...';
            const response = await api.post('/RenderTasks', {
                figureType: figureType.value,
                canvasWidth: canvasWidth.value,
                canvasHeight: canvasHeight.value,
                iterations: iterations.value
            });

            message.value = `Успіх! ${response.data.message}. ID задачі: ${response.data.taskId}`;
            await fetchTasks();

        } catch (error) {
            if (error.response) {
                let errorData = error.response.data;
                if (typeof errorData === 'object') {
                    message.value = `Помилка: ${errorData.title || JSON.stringify(errorData)}`;
                } else {
                    message.value = `Помилка: ${errorData}`;
                }
            } else {
                message.value = `Помилка з'єднання: ${error.message}`;
            }
        }
    };

    const deleteTask = async (id) => {
        if (!confirm('Ви впевнені, що хочете видалити/скасувати це завдання?')) return;
        try {
            await api.delete(`/RenderTasks/${id}`);
            await fetchTasks(); 
        } catch (error) {
            console.error("Помилка видалення:", error);
            alert("Не вдалося видалити завдання.");
        }
    };
</script>

<template>
    <div class="container">
        <h1>Рендерер параметричних кривих</h1>

        <form @submit.prevent="submitTask" class="render-form">
            <div class="form-group">
                <label>Тип фігури:</label>
                <select v-model="figureType">
                    <option value="Epicycloid">Епіциклоїда</option>
                    <option value="Lissajous">Фігура Ліссажу</option>
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
            <h2>Список завдань</h2>
            <table>
                <thead>
                    <tr>
                        <th>ID</th>
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
                        <td>{{ task.id.substring(0, 8) }}...</td>
                        <td>{{ task.figureType }}</td>
                        <td>{{ task.canvasWidth }} x {{ task.canvasHeight }}</td>
                        <td>{{ task.iterations }}</td>
                        <td>{{ task.status }}</td>
                        <td>
                            <img v-if="task.resultImageUrl" :src="`http://localhost:5000${task.resultImageUrl}`" alt="Крива" height="60" style="border-radius: 4px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); cursor: pointer;" title="Відкрити зображення" onclick="window.open(this.src, '_blank')" />
                            <span v-else>—</span>
                        </td>
                        <td>
                            <button @click="deleteTask(task.id)" class="delete-btn">Видалити</button>
                        </td>
                    </tr>
                </tbody>
            </table>
        </div>
    </div>
</template>

<style scoped>
    .container {
        max-width: 600px;
        margin: 40px auto;
        padding: 20px;
        font-family: 'Montserrat', sans-serif;
    }

    h1 {
        color: #61344B;
        text-align: center;
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

    .status-message {
        margin-top: 24px;
        padding: 16px;
        background-color: #EFD6AC;
        color: #61344B;
        border-radius: 8px;
        font-weight: 600;
        text-align: center;
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