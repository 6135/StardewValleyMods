#generate code that prints an array to a file with the options for a dropdown ranging from 0 to 1 in x increments
import json

def generate_drop_down_options(min, max, increment):
    options = []
    for i in range(int((max - min) / increment) + 1):
        value = min + i * increment
        #truncate to 3 decimal places
        value = float("{:.3f}".format(value))
        options.append(value)
    return options

def write_options_to_file(options, filename):
    with open(filename, 'w') as file:
        file.write(json.dumps(options))

options = generate_drop_down_options(0, 1, 0.005)
print(options)
write_options_to_file(options, 'dropdownOptions.json')