FROM python:3.14-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY server.py overture_service.py ./
RUN useradd --uid 10001 --create-home maps && mkdir /data && chown maps /data
USER maps
EXPOSE 8787
CMD ["python", "server.py", "--host", "0.0.0.0", "--cache", "/data/sectors.sqlite"]
