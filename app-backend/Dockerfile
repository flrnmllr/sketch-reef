FROM python:3

RUN pip install --upgrade pip

WORKDIR /usr/src/app

COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

COPY requirements.txt ./

COPY . .

CMD [ "python", "./app.py" ]